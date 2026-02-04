using Amazon.S3.Model;
using Amazon.S3;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Threading.Tasks;
using System.Threading;

namespace AF.Umbraco.S3.Media.Storage.Services
{

    /// <summary>
    /// Verifies S3 bucket connectivity at startup and blocks application boot on failure.
    /// </summary>
    internal sealed class AWSS3StartupConnectivityHostedService(
        IAmazonS3 s3Client,
        IOptionsMonitor<AWSS3FileSystemOptions> options,
        ILogger<AWSS3StartupConnectivityHostedService> logger) : IHostedService
    {
        /// <summary>
        /// Gets the log prefix used by this component.
        /// </summary>
        private const string LogPrefix = "[AFUS3MS]";
        /// <summary>
        /// Gets the s 3 client used by this component.
        /// </summary>
        private readonly IAmazonS3 _s3Client = s3Client;
        /// <summary>
        /// Gets the options used by this component.
        /// </summary>
        private readonly IOptionsMonitor<AWSS3FileSystemOptions> _options = options;
        /// <summary>
        /// Gets the logger used by this component.
        /// </summary>
        private readonly ILogger<AWSS3StartupConnectivityHostedService> _logger = logger;

        /// <summary>
        /// Validates startup connectivity against the configured media bucket.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task that completes when validation is finished.</returns>
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            AWSS3FileSystemOptions mediaOptions = _options.Get(AWSS3FileSystemOptions.MediaFileSystemName);
            string bucketName = mediaOptions.BucketName;

            if (string.IsNullOrWhiteSpace(bucketName))
            {
                string message = $"{LogPrefix} Missing required bucket configuration for {AWSS3FileSystemOptions.MediaFileSystemName}.";
                _logger.LogCritical(message);
                throw new InvalidOperationException(message);
            }

            try
            {
                await _s3Client.GetBucketLocationAsync(
                    new GetBucketLocationRequest { BucketName = bucketName },
                    cancellationToken).ConfigureAwait(false);

                _logger.LogInformation("{LogPrefix} S3 startup connectivity check passed for bucket {BucketName}.", LogPrefix, bucketName);
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "{LogPrefix} S3 startup connectivity check failed for bucket {BucketName}. Application startup is blocked.", LogPrefix, bucketName);
                throw;
            }
        }

        /// <summary>
        /// Stops the hosted service.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A completed task.</returns>
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
