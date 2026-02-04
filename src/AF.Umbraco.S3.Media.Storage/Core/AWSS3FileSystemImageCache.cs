using Amazon.Extensions.NETCore.Setup;
using Amazon.Runtime.CredentialManagement;
using Amazon.Runtime;
using Amazon.S3.Model;
using Amazon.S3;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp.Web.Caching.AWS;
using SixLabors.ImageSharp.Web.Caching;
using SixLabors.ImageSharp.Web.Resolvers;
using SixLabors.ImageSharp.Web;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Resources;
using System.Threading.Tasks;
using System.Threading;

namespace AF.Umbraco.S3.Media.Storage.Core
{

    /// <summary>
    /// Persists ImageSharp cache entries in S3 and applies cache retention cleanup.
    /// </summary>
    public class AWSS3FileSystemImageCache : IImageCache
    {
        /// <summary>
        /// Standard log prefix used by this package.
        /// </summary>
        private const string LogPrefix = "[AFUS3MS]";
        /// <summary>
        /// Root folder used for cached image objects in S3.
        /// </summary>
        private const string _cachePath = "cache/";
        /// <summary>
        /// Named filesystem registration used to resolve options.
        /// </summary>
        private readonly string _name;
        /// <summary>
        /// Underlying ImageSharp S3 cache implementation.
        /// </summary>
        private AWSS3StorageCache baseCache = null;
        /// <summary>
        /// Application configuration used to resolve AWS settings.
        /// </summary>
        private readonly IConfiguration _configuration;
        /// <summary>
        /// Shared AWS S3 client.
        /// </summary>
        private readonly IAmazonS3 _s3Client;
        /// <summary>
        /// Service provider used by ImageSharp cache internals.
        /// </summary>
        private readonly IServiceProvider _serviceProvider;
        /// <summary>
        /// Logger for operational diagnostics.
        /// </summary>
        private readonly ILogger<AWSS3FileSystemImageCache> _logger;
        /// <summary>
        /// Target bucket name used for cache operations.
        /// </summary>
        private string _bucketName = string.Empty;
        /// <summary>
        /// Indicates whether retention cleanup is enabled.
        /// </summary>
        private bool _cacheRetentionEnabled = true;
        /// <summary>
        /// Maximum age allowed for cached entries before cleanup.
        /// </summary>
        private TimeSpan _cacheRetentionMaxAge = TimeSpan.FromDays(90);
        /// <summary>
        /// Minimum interval between retention sweeps.
        /// </summary>
        private TimeSpan _cacheRetentionSweepInterval = TimeSpan.FromHours(12);
        /// <summary>
        /// UTC timestamp for the next scheduled retention sweep.
        /// </summary>
        private DateTimeOffset _nextRetentionSweepUtc = DateTimeOffset.MinValue;
        /// <summary>
        /// Prevents concurrent retention sweeps.
        /// </summary>
        private readonly SemaphoreSlim _retentionSweepLock = new(1, 1);
        /// <summary>
        /// Resource manager used for localized user-facing messages.
        /// </summary>
        private static readonly ResourceManager ResourceManager =
            new("AF.Umbraco.S3.Media.Storage.Core.AWSS3FileSystem", typeof(AWSS3FileSystemImageCache).Assembly);

        /// <summary>
        /// Initializes a new instance of the <see cref="AWSS3FileSystemImageCache" /> class.
        /// </summary>
        /// <param name="options">The options.</param>
        public AWSS3FileSystemImageCache(
            IOptionsMonitor<AWSS3FileSystemOptions> options,
            IConfiguration configuration,
            IAmazonS3 s3Client,
            IServiceProvider serviceProvider,
            ILogger<AWSS3FileSystemImageCache> logger)
            : this(AWSS3FileSystemOptions.MediaFileSystemName, options, configuration, s3Client, serviceProvider, logger)
        {
        }

        /// <summary>
        /// Creates a new instance of <see cref="AWSS3FileSystemImageCache" />.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="options">The options.</param>
        /// <exception cref="System.ArgumentNullException">options
        /// or.
        /// name</exception>
        protected AWSS3FileSystemImageCache(
            string name,
            IOptionsMonitor<AWSS3FileSystemOptions> options,
            IConfiguration configuration,
            IAmazonS3 s3Client,
            IServiceProvider serviceProvider,
            ILogger<AWSS3FileSystemImageCache> logger)
        {
            _name = name ?? throw new ArgumentNullException(nameof(name));

            if (options == null) throw new ArgumentNullException(nameof(options));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

            _s3Client = s3Client;
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Collect configurations
            var fileSystemOptions = options.Get(name);

            ApplyCacheRetentionSettings(fileSystemOptions);

            AWSOptions awsOptions = _configuration.GetAWSOptions();
            AWSS3StorageCacheOptions cacheOptions = getAWSS3StorageCacheOptions(fileSystemOptions, awsOptions);

            baseCache = new AWSS3StorageCache(Microsoft.Extensions.Options.Options.Create(cacheOptions), _serviceProvider);

            options.OnChange(OptionsOnChange);
        }

        /// <summary>
        /// Retrieves an existing cache entry for the provided key.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns>A task containing the cache resolver for the key.</returns>
        public async Task<IImageCacheResolver> GetAsync(string key)
        {
            string cacheAndKey = Path.Combine(_cachePath, key);

            return await baseCache.GetAsync(cacheAndKey);
        }

        /// <summary>
        /// Stores a cache entry and triggers retention cleanup when due.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="stream">The stream.</param>
        /// <param name="metadata">The metadata.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public Task SetAsync(string key, Stream stream, ImageCacheMetadata metadata)
        {
            string cacheAndKey = Path.Combine(_cachePath, key);
            return SetAndCleanupAsync(cacheAndKey, stream, metadata);
        }

        /// <summary>
        /// Rebuilds cache configuration when named filesystem options change.
        /// </summary>
        private void OptionsOnChange(AWSS3FileSystemOptions options, string name)
        {
            if (name != _name) return;

            AWSOptions awsOptions = _configuration.GetAWSOptions();
            var cacheOptions = getAWSS3StorageCacheOptions(options, awsOptions);
            ApplyCacheRetentionSettings(options);

            baseCache = new AWSS3StorageCache(Microsoft.Extensions.Options.Options.Create(cacheOptions), _serviceProvider);
        }

        /// <summary>
        /// Writes a cache entry and then applies retention cleanup if required.
        /// </summary>
        private async Task SetAndCleanupAsync(string cacheAndKey, Stream stream, ImageCacheMetadata metadata)
        {
            try
            {
                await baseCache.SetAsync(cacheAndKey, stream, metadata).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                string fileName = Path.GetFileName(cacheAndKey);
                string userFriendlyName = string.IsNullOrWhiteSpace(fileName) ? "image" : fileName;

                _logger.LogError(ex,
                    "{LogPrefix} S3 cache write failed. Bucket={Bucket}; CacheKey={CacheKey}; ExceptionType={ExceptionType}; InnerExceptionType={InnerExceptionType}; InnerExceptionMessage={InnerExceptionMessage}",
                    LogPrefix,
                    _bucketName,
                    cacheAndKey,
                    ex.GetType().FullName,
                    ex.InnerException?.GetType().FullName ?? "none",
                    ex.InnerException?.Message ?? "none");

                throw new AWSS3UserAlertException(GetLocalizedMessage("CacheToS3FailedMessage", userFriendlyName), ex);
            }

            await CleanupExpiredCacheIfNeededAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// Resolves the AWS region used by the cache client.
        /// </summary>
        private string getRegionName(AWSS3FileSystemOptions options)
        {
            // Get region -- start with fileSystemOptions; Doesn't exist? fallback to AWSOptions and then to S3Client
            string region = options.Region;
            if (string.IsNullOrEmpty(region))
            {
                AWSOptions awsOptions = _configuration.GetAWSOptions();
                if (awsOptions != null && awsOptions.Region != null)
                {
                    region = awsOptions.Region.SystemName;
                }
                else if (_s3Client != null)
                {
                    region = _s3Client.Config?.RegionEndpoint?.SystemName;
                }
            }

            return region;
        }

        /// <summary>
        /// Builds ImageSharp S3 cache options from package settings and AWS credential sources.
        /// </summary>
        /// <param name="awss3FileSystemOptions">Named S3 filesystem options.</param>
        /// <param name="awsOptions">Resolved AWS SDK options.</param>
        /// <returns>The configured ImageSharp S3 cache options.</returns>
        private AWSS3StorageCacheOptions getAWSS3StorageCacheOptions(AWSS3FileSystemOptions awss3FileSystemOptions, AWSOptions awsOptions)
        {
            AWSS3StorageCacheOptions cacheOptions = new()
            {
                BucketName = awss3FileSystemOptions.BucketName,
                Region = getRegionName(awss3FileSystemOptions)
            };

            // Respect custom S3-compatible endpoints (eg. MinIO) for thumbnail cache too.
            string endpoint = _configuration["AWS:ServiceURL"] ?? _configuration["AWS:ServiceUrl"];
            if (!string.IsNullOrWhiteSpace(endpoint))
            {
                cacheOptions.Endpoint = endpoint;
            }

            // Prefer explicit environment credentials when running locally/in containers.
            string envAccessKey = Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID");
            string envSecretKey = Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY");
            if (!string.IsNullOrWhiteSpace(envAccessKey) && !string.IsNullOrWhiteSpace(envSecretKey))
            {
                cacheOptions.AccessKey = envAccessKey;
                cacheOptions.AccessSecret = envSecretKey;
            }

            // If profile is configured, resolve credentials from shared profile store.
            if (!string.IsNullOrWhiteSpace(awsOptions.Profile))
            {
                var chain = string.IsNullOrWhiteSpace(awsOptions.ProfilesLocation)
                    ? new CredentialProfileStoreChain()
                    : new CredentialProfileStoreChain(awsOptions.ProfilesLocation);

                if (chain.TryGetAWSCredentials(awsOptions.Profile, out AWSCredentials awsCredentials))
                {
                    cacheOptions.AccessKey = awsCredentials.GetCredentials().AccessKey;
                    cacheOptions.AccessSecret = awsCredentials.GetCredentials().SecretKey;
                }
            }

            return cacheOptions;
        }

        /// <summary>
        /// Applies retention settings from configuration to runtime fields.
        /// </summary>
        private void ApplyCacheRetentionSettings(AWSS3FileSystemOptions options)
        {
            _bucketName = options.BucketName ?? string.Empty;
            AWSS3CacheRetentionOptions retention = options.CacheRetention ?? new AWSS3CacheRetentionOptions();

            if (retention.TestModeEnable)
            {
                _cacheRetentionEnabled = true;
                _cacheRetentionMaxAge = TimeSpan.FromMinutes(retention.TestModeMaxAgeMinutes > 0 ? retention.TestModeMaxAgeMinutes : 10);
                _cacheRetentionSweepInterval = TimeSpan.FromSeconds(retention.TestModeSweepSeconds > 0 ? retention.TestModeSweepSeconds : 30);
                return;
            }

            _cacheRetentionEnabled = retention.Enabled;
            _cacheRetentionMaxAge = TimeSpan.FromDays(retention.NumberOfDays > 0 ? retention.NumberOfDays : 90);
            _cacheRetentionSweepInterval = TimeSpan.FromHours(12);
        }

        /// <summary>
        /// Deletes expired cache objects when retention is enabled and the sweep interval is reached.
        /// </summary>
        private async Task CleanupExpiredCacheIfNeededAsync()
        {
            if (!_cacheRetentionEnabled)
            {
                return;
            }

            DateTimeOffset now = DateTimeOffset.UtcNow;
            if (now < _nextRetentionSweepUtc)
            {
                return;
            }

            if (!await _retentionSweepLock.WaitAsync(0).ConfigureAwait(false))
            {
                return;
            }

            try
            {
                if (DateTimeOffset.UtcNow < _nextRetentionSweepUtc)
                {
                    return;
                }

                DateTime cutoff = DateTime.UtcNow.Subtract(_cacheRetentionMaxAge);
                string[] prefixes = [_cachePath, "cache/cache/"];
                var expired = new List<KeyVersion>();

                if (string.IsNullOrWhiteSpace(_bucketName))
                {
                    _nextRetentionSweepUtc = DateTimeOffset.UtcNow.Add(_cacheRetentionSweepInterval);
                    return;
                }

                foreach (string prefix in prefixes)
                {
                    var listRequest = new ListObjectsRequest
                    {
                        BucketName = _bucketName,
                        Prefix = prefix
                    };

                    ListObjectsResponse listResponse;
                    do
                    {
                        listResponse = await _s3Client.ListObjectsAsync(listRequest).ConfigureAwait(false);

                        expired.AddRange((listResponse.S3Objects ?? Enumerable.Empty<S3Object>())
                            .Where(o => o.LastModified < cutoff)
                            .Select(o => new KeyVersion { Key = o.Key }));

                        listRequest.Marker = listResponse.NextMarker;
                    } while (listResponse.IsTruncated == true);
                }

                const int batchSize = 1000;
                foreach (KeyVersion[] batch in expired.Chunk(batchSize))
                {
                    var deleteRequest = new DeleteObjectsRequest
                    {
                        BucketName = _bucketName,
                        Objects = [.. batch]
                    };

                    await _s3Client.DeleteObjectsAsync(deleteRequest).ConfigureAwait(false);
                }

                _nextRetentionSweepUtc = DateTimeOffset.UtcNow.Add(_cacheRetentionSweepInterval);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "{LogPrefix} Unable to apply S3 cache retention cleanup.", LogPrefix);
                _nextRetentionSweepUtc = DateTimeOffset.UtcNow.AddMinutes(1);
            }
            finally
            {
                _retentionSweepLock.Release();
            }
        }

        /// <summary>
        /// Resolves a localized message from resource files with English fallback.
        /// </summary>
        private static string GetLocalizedMessage(string key, params object[] args)
        {
            var localizedText = ResourceManager.GetString(key, CultureInfo.CurrentUICulture)
                ?? ResourceManager.GetString(key, CultureInfo.GetCultureInfo("en-US"))
                ?? "S3 cache operation failed for '{0}'.";

            return string.Format(CultureInfo.CurrentUICulture, localizedText, args);
        }
    }
}
