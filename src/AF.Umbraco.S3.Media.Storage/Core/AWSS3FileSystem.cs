using Amazon.S3.Model;
using Amazon.S3;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Resources;
using System.Security.Claims;
using Umbraco.Cms.Core.Hosting;
using Umbraco.Cms.Core.Security;

namespace AF.Umbraco.S3.Media.Storage.Core
{

    /// <summary>
    /// Implements Umbraco media filesystem operations backed by AWS S3 storage.
    /// </summary>
    public class AWSS3FileSystem : IAWSS3FileSystem
    {
        /// <summary>
        /// Gets the log prefix used by this component.
        /// </summary>
        private const string LogPrefix = "[AFUS3MS]";
        /// <summary>
        /// Gets the content type provider used by this component.
        /// </summary>
        private readonly IContentTypeProvider _contentTypeProvider;
        /// <summary>
        /// Gets the s 3 client used by this component.
        /// </summary>
        private readonly IAmazonS3 _S3Client;
        /// <summary>
        /// Stores the root URL.
        /// </summary>
        private readonly string _rootUrl;
        /// <summary>
        /// Gets the bucket prefix used by this component.
        /// </summary>
        private readonly string _bucketPrefix;
        /// <summary>
        /// Gets the bucket name used by this component.
        /// </summary>
        private readonly string _bucketName;
        /// <summary>
        /// Gets the root path used by this component.
        /// </summary>
        private readonly string _rootPath;
        /// <summary>
        /// Gets the logger used by this component.
        /// </summary>
        protected readonly ILogger<AWSS3FileSystem> _logger;
        /// <summary>
        /// Gets the mime type resolver used by this component.
        /// </summary>
        private readonly IMimeTypeResolver _mimeTypeResolver;
        /// <summary>
        /// Stores the HTTP context accessor.
        /// </summary>
        private readonly IHttpContextAccessor _httpContextAccessor;
        /// <summary>
        /// Gets the canned acl used by this component.
        /// </summary>
        private readonly S3CannedACL _cannedACL;
        /// <summary>
        /// Gets the server side encryption method used by this component.
        /// </summary>
        private readonly ServerSideEncryptionMethod _serverSideEncryptionMethod;
        /// <summary>
        /// Gets the resource manager used by this component.
        /// </summary>
        private static readonly ResourceManager ResourceManager =
            new(typeof(AWSS3FileSystem).FullName ?? "AF.Umbraco.S3.Media.Storage.Core.AWSS3FileSystem", typeof(AWSS3FileSystem).Assembly);

        /// <summary>
        /// Gets the delimiter used by this component.
        /// </summary>
        protected const string Delimiter = "/";
        /// <summary>
        /// Gets the batch size used by this component.
        /// </summary>
        protected const int BatchSize = 1000;

        /// <summary>
        /// Gets a value indicating whether physical-path adds are supported.
        /// </summary>
        public bool CanAddPhysical => false;

        /// <summary>
        /// Creates a new instance of <see cref="AWSS3FileSystem" />.
        /// </summary>
        /// <param name="options">The configured filesystem options.</param>
        /// <param name="hostingEnvironment">The hosting environment used to resolve virtual paths.</param>
        /// <param name="contentTypeProvider">The content type provider for extension lookups.</param>
        /// <param name="logger">The filesystem logger.</param>
        /// <param name="mimeTypeResolver">The MIME type resolver.</param>
        /// <param name="s3Client">The AWS S3 client.</param>
        /// <param name="httpContextAccessor">Accessor used to resolve request culture information.</param>
        /// <exception cref="ArgumentNullException">Thrown when required dependencies are null.</exception>
        public AWSS3FileSystem(AWSS3FileSystemOptions options, IHostingEnvironment hostingEnvironment,
            IContentTypeProvider contentTypeProvider, ILogger<AWSS3FileSystem> logger, IMimeTypeResolver mimeTypeResolver, IAmazonS3 s3Client, IHttpContextAccessor httpContextAccessor)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (hostingEnvironment == null) throw new ArgumentNullException(nameof(hostingEnvironment));

            _logger = logger;
            _contentTypeProvider = contentTypeProvider ?? throw new ArgumentNullException(nameof(contentTypeProvider));
            _bucketName = options.BucketName ?? throw new ArgumentNullException(nameof(contentTypeProvider));

            _rootUrl = EnsureUrlSeparatorChar(hostingEnvironment.ToAbsolute(options.VirtualPath)).TrimEnd('/');
            _bucketPrefix = AWSS3FileSystemOptions.BucketPrefix ?? _rootUrl;
            _cannedACL = options.CannedACL;
            _serverSideEncryptionMethod = options.ServerSideEncryptionMethod;
            _rootPath = hostingEnvironment.ToAbsolute(options.VirtualPath);

            _mimeTypeResolver = mimeTypeResolver;
            _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));

            _S3Client = s3Client;
        }

        /// <summary>
        /// Gets s3 Client.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <returns>The result of the operation.</returns>
        public IAmazonS3 GetS3Client(string path)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));

            return _S3Client;
        }

        /// <summary>
        /// Ensures url Separator Char.
        /// </summary>
        private static string EnsureUrlSeparatorChar(string path)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));

            return path.Replace("\\", "/", StringComparison.InvariantCultureIgnoreCase);
        }

        /// <summary>
        /// Gets directories.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <returns>The result of the operation.</returns>
        public IEnumerable<string> GetDirectories(string path)
        {
            if (string.IsNullOrEmpty(path))
                path = "/";

            path = ResolveBucketPath(path, true);
            var request = new ListObjectsRequest
            {
                BucketName = _bucketName,
                Delimiter = Delimiter,
                Prefix = path
            };

            var response = ExecuteWithContinuation(request);
            return [.. response
                .SelectMany(p => p.CommonPrefixes ?? Enumerable.Empty<string>())
                .Select(p => RemovePrefix(p))];
        }

        /// <summary>
        /// Deletes directory.
        /// </summary>
        /// <param name="path">The path.</param>
        public void DeleteDirectory(string path)
        {
            DeleteDirectory(path, false);
        }

        /// <summary>
        /// Deletes directory.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <param name="recursive">The recursive.</param>
        public void DeleteDirectory(string path, bool recursive)
        {
            try
            {
                string directoryPrefix = ResolveBucketPath(path, true);

                string[] deletedSourcePaths = [.. GetKeysByPrefix(directoryPrefix)
                    .Select(key => key.TrimStart(Delimiter.ToCharArray()))
                    .Distinct(StringComparer.OrdinalIgnoreCase)];

                DeleteObjectsByPrefix(directoryPrefix);

                foreach (string sourcePath in deletedSourcePaths)
                {
                    DeleteMirroredCacheObjectBySourcePath(sourcePath);
                    DeleteImageCacheBySourcePath(sourcePath);
                }
            }
            catch (Exception ex)
            {
                throw CreateUserAlertException("DeleteFromS3FailedMessage", "delete directory", path, ex);
            }
        }

        /// <summary>
        /// Gets the name used by this component.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <returns>The result of the operation.</returns>
        public bool DirectoryExists(string path)
        {
            var request = new ListObjectsRequest
            {
                BucketName = _bucketName,
                Prefix = ResolveBucketPath(path, true),
                MaxKeys = 1
            };

            var response = Execute(client => client.ListObjectsAsync(request)).Result;
            return response.S3Objects.Count > 0;
        }

        /// <summary>
        /// Adds file.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <param name="stream">The stream.</param>
        public void AddFile(string path, Stream stream)
        {
            AddFile(path, stream, true);
        }

        /// <summary>
        /// Adds file.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <param name="stream">The stream.</param>
        /// <param name="overrideIfExists">The overrideIfExists.</param>
        public void AddFile(string path, Stream stream, bool overrideIfExists)
        {
            if (!overrideIfExists && FileExists(path))
            {
                throw new InvalidOperationException($"A file at path '{path}' already exists");
            }

            if (stream.CanSeek)
            {
                stream.Position = 0;
            }

            using var memoryStream = new MemoryStream();
            stream.CopyTo(memoryStream);
            memoryStream.Position = 0;
            ValidateImageIfNeeded(path, memoryStream);
            memoryStream.Position = 0;
            AddFileFromStream(path, memoryStream);
        }

        /// <summary>
        /// Adds file.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <param name="physicalPath">The physicalPath.</param>
        /// <param name="overrideIfExists">The overrideIfExists.</param>
        /// <param name="copy">The copy.</param>
        public void AddFile(string path, string physicalPath, bool overrideIfExists = true, bool copy = false)
        {
            if (!overrideIfExists && FileExists(path))
            {
                throw new InvalidOperationException($"A file at path '{path}' already exists");
            }

            using (var fileStream = new FileStream(physicalPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                ValidateImageIfNeeded(path, fileStream);
            }

            var request = new PutObjectRequest
            {
                BucketName = _bucketName,
                Key = ResolveBucketPath(path),
                CannedACL = _cannedACL,
                ContentType = _mimeTypeResolver.Resolve(path),
                FilePath = physicalPath,
                ServerSideEncryptionMethod = _serverSideEncryptionMethod
            };

            try
            {
                Execute(client => client.PutObjectAsync(request)).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                throw CreateUserAlertException("UploadToS3FailedMessage", "upload from physical path", path, ex);
            }

            try
            {
                AddMirroredCacheFromPhysical(path, physicalPath);
            }
            catch (Exception ex)
            {
                throw CreateUserAlertException("CacheToS3FailedMessage", "cache from physical path", path, ex);
            }
        }

        /// <summary>
        /// Validates image If Needed.
        /// </summary>
        private void ValidateImageIfNeeded(string path, Stream stream)
        {
            var contentType = _mimeTypeResolver.Resolve(path);
            if (!contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            long initialPosition = 0;
            if (stream.CanSeek)
            {
                initialPosition = stream.Position;
                stream.Position = 0;
            }

            try
            {
                IImageFormat format = Image.DetectFormat(stream) ?? throw new InvalidOperationException("unable to detect image format");
            }
            catch (Exception ex) when (ex is UnknownImageFormatException || ex is InvalidImageContentException || ex is InvalidOperationException)
            {
                var fileName = Path.GetFileName(path);

                _logger.LogWarning(ex,
                    "{LogPrefix} Invalid image rejected. Path={Path}; FileName={FileName}; ExceptionType={ExceptionType}",
                    LogPrefix,
                    path,
                    fileName,
                    ex.GetType().FullName);

                throw new InvalidOperationException(
                    GetLocalizedMessage("InvalidImageFileMessage", fileName),
                    ex);
            }
            finally
            {
                if (stream.CanSeek)
                {
                    stream.Position = initialPosition;
                }
            }
        }

        /// <summary>
        /// Gets localized Message.
        /// </summary>
        private string GetLocalizedMessage(string key, params object[] args)
        {
            CultureInfo uiCulture = ResolveUiCulture();
            var localizedText = ResourceManager.GetString(key, uiCulture)
                ?? ResourceManager.GetString(key, CultureInfo.GetCultureInfo("en-US"))
                ?? "An error occurred while processing S3 media storage.";

            return SafeFormat(uiCulture, localizedText, args);
        }

        /// <summary>
        /// Adds file From Stream.
        /// </summary>
        private void AddFileFromStream(string path, Stream inputStream)
        {
            if (inputStream.CanSeek)
            {
                inputStream.Position = 0;
            }

            var request = new PutObjectRequest
            {
                BucketName = _bucketName,
                Key = ResolveBucketPath(path),
                CannedACL = _cannedACL,
                ContentType = _mimeTypeResolver.Resolve(path),
                InputStream = inputStream,
                AutoCloseStream = false,
                ServerSideEncryptionMethod = _serverSideEncryptionMethod
            };

            try
            {
                Execute(client => client.PutObjectAsync(request)).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                throw CreateUserAlertException("UploadToS3FailedMessage", "upload", path, ex);
            }

            try
            {
                AddMirroredCacheFromStream(path, inputStream);
            }
            catch (Exception ex)
            {
                if (!IsImageExtension(path))
                {
                    return;
                }

                throw CreateUserAlertException("CacheToS3FailedMessage", "cache from stream", path, ex);
            }
        }

        /// <summary>
        /// Gets files.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <returns>The result of the operation.</returns>
        public IEnumerable<string> GetFiles(string path)
        {
            return GetFiles(path, "*.*");
        }

        /// <summary>
        /// Gets files.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <param name="filter">The filter.</param>
        /// <returns>The result of the operation.</returns>
        public IEnumerable<string> GetFiles(string path, string filter)
        {
            path = ResolveBucketPath(path, true);

            string filename = Path.GetFileNameWithoutExtension(filter);
            if (filename.EndsWith("*"))
                filename = filename[..^1];

            string ext = Path.GetExtension(filter);
            if (ext.Contains("*"))
                ext = string.Empty;

            var request = new ListObjectsRequest
            {
                BucketName = _bucketName,
                Delimiter = Delimiter,
                Prefix = path + filename
            };

            var response = ExecuteWithContinuation(request);
            return [.. response
                .SelectMany(p => p.S3Objects ?? Enumerable.Empty<S3Object>())
                .Select(p => RemovePrefix(p.Key))
                .Where(p => !string.IsNullOrEmpty(p) && p.EndsWith(ext))];
        }

        /// <summary>
        /// Opens file.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <returns>The result of the operation.</returns>
        public Stream OpenFile(string path)
        {

            var request = new GetObjectRequest
            {
                BucketName = _bucketName,
                Key = ResolveBucketPath(path)
            };

            MemoryStream stream;
            using (var response = Execute(client => client.GetObjectAsync(request)).Result)
            {
                stream = new MemoryStream();
                response.ResponseStream.CopyTo(stream);
            }

            if (stream.CanSeek)
                stream.Seek(0, SeekOrigin.Begin);

            return stream;
        }

        /// <summary>
        /// Deletes file.
        /// </summary>
        /// <param name="path">The path.</param>
        public void DeleteFile(string path)
        {
            try
            {
                string resolvedPath = ResolveBucketPath(path).TrimStart(Delimiter.ToCharArray());
                var request = new DeleteObjectRequest
                {
                    BucketName = _bucketName,
                    Key = resolvedPath
                };
                Execute(client => client.DeleteObjectAsync(request));

                DeleteMirroredCacheObjectBySourcePath(resolvedPath);
                DeleteImageCacheBySourcePath(resolvedPath);
            }
            catch (Exception ex)
            {
                throw CreateUserAlertException("DeleteFromS3FailedMessage", "delete file", path, ex);
            }
        }

        /// <summary>
        /// Gets the name used by this component.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <returns>The result of the operation.</returns>
        public bool FileExists(string path)
        {
            var request = new GetObjectMetadataRequest
            {
                BucketName = _bucketName,
                Key = ResolveBucketPath(path)
            };

            try
            {
                var response = Execute(client => client.GetObjectMetadataAsync(request)).GetAwaiter().GetResult();
                return true;
            }
            catch (FileNotFoundException)
            {
                return false;
            }
            catch (AmazonS3Exception ex)
            {
                if (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return false;
                }

                throw;
            }
        }

        /// <summary>
        /// Gets relative Path.
        /// </summary>
        /// <param name="fullPathOrUrl">The fullPathOrUrl.</param>
        /// <returns>The result of the operation.</returns>
        public string GetRelativePath(string fullPathOrUrl)
        {
            if (string.IsNullOrEmpty(fullPathOrUrl))
                return string.Empty;

            //Strip protocol if not in hostname
            if (!_bucketName.StartsWith("http"))
            {
                if (fullPathOrUrl.StartsWith("https://"))
                {
                    fullPathOrUrl = fullPathOrUrl["https://".Length..];
                }
                if (fullPathOrUrl.StartsWith("http://"))
                {
                    fullPathOrUrl = fullPathOrUrl["http://".Length..];
                }
            }

            //Strip Hostname
            //if (fullPathOrUrl.StartsWith(_bucketName, StringComparison.InvariantCultureIgnoreCase))
            //{
            //    fullPathOrUrl = fullPathOrUrl.Substring(Config.BucketHostName.Length);
            //    fullPathOrUrl = fullPathOrUrl.TrimStart(Delimiter.ToCharArray());
            //}

            //Strip Virtual Path
            if (fullPathOrUrl.StartsWith(_rootPath, StringComparison.InvariantCultureIgnoreCase))
            {
                fullPathOrUrl = fullPathOrUrl[_rootPath.Length..];
                fullPathOrUrl = fullPathOrUrl.TrimStart(Delimiter.ToCharArray());
            }

            //Strip Bucket Prefix
            if (fullPathOrUrl.StartsWith(_bucketPrefix, StringComparison.InvariantCultureIgnoreCase))
            {
                fullPathOrUrl = fullPathOrUrl[_bucketPrefix.Length..];
                fullPathOrUrl = fullPathOrUrl.TrimStart(Delimiter.ToCharArray());
            }

            return fullPathOrUrl;
        }

        /// <summary>
        /// Gets full Path.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <returns>The result of the operation.</returns>
        public string GetFullPath(string path)
        {
            return path;
        }

        /// <summary>
        /// Gets url.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <returns>The result of the operation.</returns>
        public string GetUrl(string path)
        {
            var hostName = "";

            return string.Concat(hostName, "/", ResolveBucketPath(path));
        }

        /// <summary>
        /// Gets last Modified.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <returns>The result of the operation.</returns>
        public DateTimeOffset GetLastModified(string path)
        {
            var request = new GetObjectMetadataRequest
            {
                BucketName = _bucketName,
                Key = ResolveBucketPath(path)
            };

            var response = Execute(client => client.GetObjectMetadataAsync(request)).Result;
            return new DateTimeOffset(response.LastModified ?? DateTime.UtcNow);
        }

        /// <summary>
        /// Gets created.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <returns>The result of the operation.</returns>
        public DateTimeOffset GetCreated(string path)
        {
            //It Is Not Possible To Get Object Created Date - Bucket Versioning Required
            //Return Last Modified Date Instead
            return GetLastModified(path);
        }

        /// <summary>
        /// Gets size.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <returns>The result of the operation.</returns>
        public long GetSize(string path)
        {
            var request = new GetObjectMetadataRequest
            {
                BucketName = _bucketName,
                Key = ResolveBucketPath(path)
            };

            var response = Execute(client => client.GetObjectMetadataAsync(request)).Result;
            return response.ContentLength;
        }

        /// <summary>
        /// Gets the name used by this component.
        /// </summary>
        /// <typeparam name="T">The request result type.</typeparam>
        /// <param name="request">The request delegate to execute.</param>
        /// <returns>The result returned by the request delegate.</returns>
        protected virtual T Execute<T>(Func<IAmazonS3, T> request)
        {
            try
            {
                return request(_S3Client);
            }
            catch (AmazonS3Exception ex)
            {
                if (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                    throw new FileNotFoundException(ex.Message, ex);
                if (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    throw new UnauthorizedAccessException(ex.Message, ex);

                _logger.LogError(ex, "{LogPrefix} S3 request failed. ErrorCode={ErrorCode}, Message={Message}.", LogPrefix, ex.ErrorCode, ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Gets the name used by this component.
        /// </summary>
        /// <param name="request">The initial list request.</param>
        /// <returns>An enumeration of paged S3 list responses.</returns>
        protected virtual IEnumerable<ListObjectsResponse> ExecuteWithContinuation(ListObjectsRequest request)
        {

            var response = Execute(client => client.ListObjectsAsync(request)).Result;
            yield return response;

            while (response.IsTruncated == true)
            {
                request.Marker = response.NextMarker;
                response = Execute(client => client.ListObjectsAsync(request)).Result;
                yield return response;
            }
        }

        /// <summary>
        /// Deletes objects By Prefix.
        /// </summary>
        private void DeleteObjectsByPrefix(string prefix)
        {
            if (string.IsNullOrWhiteSpace(prefix))
            {
                return;
            }

            var listRequest = new ListObjectsRequest
            {
                BucketName = _bucketName,
                Prefix = prefix
            };

            var keys = ExecuteWithContinuation(listRequest)
                .SelectMany(p => p.S3Objects ?? Enumerable.Empty<S3Object>())
                .Select(p => new KeyVersion { Key = p.Key })
                .ToArray();

            foreach (KeyVersion[] batch in keys.Chunk(BatchSize))
            {
                var deleteRequest = new DeleteObjectsRequest
                {
                    BucketName = _bucketName,
                    Objects = [.. batch]
                };

                Execute(client => client.DeleteObjectsAsync(deleteRequest)).GetAwaiter().GetResult();
            }
        }

        /// <summary>
        /// Creates user Alert Exception.
        /// </summary>
        private AWSS3UserAlertException CreateUserAlertException(string localizedMessageKey, string operation, string path, Exception ex)
        {
            string fileName = Path.GetFileName(path);
            string userFriendlyPath = string.IsNullOrWhiteSpace(fileName) ? path : fileName;

            _logger.LogError(ex,
                "{LogPrefix} S3 operation failed. Operation={Operation}; Bucket={Bucket}; RootUrl={RootUrl}; Path={Path}; ExceptionType={ExceptionType}; InnerExceptionType={InnerExceptionType}; InnerExceptionMessage={InnerExceptionMessage}",
                LogPrefix,
                operation,
                _bucketName,
                _rootUrl,
                path,
                ex.GetType().FullName,
                ex.InnerException?.GetType().FullName ?? "none",
                ex.InnerException?.Message ?? "none");

            return new AWSS3UserAlertException(GetLocalizedMessage(localizedMessageKey, userFriendlyPath), ex);
        }

        /// <summary>
        /// Adds mirrored Cache From Stream.
        /// </summary>
        private void AddMirroredCacheFromStream(string path, Stream inputStream)
        {
            if (!ShouldCacheImage(path, inputStream))
            {
                return;
            }

            if (inputStream.CanSeek)
            {
                inputStream.Position = 0;
            }

            var cacheRequest = new PutObjectRequest
            {
                BucketName = _bucketName,
                Key = BuildMirroredCacheKey(ResolveBucketPath(path)),
                CannedACL = _cannedACL,
                ContentType = _mimeTypeResolver.Resolve(path),
                InputStream = inputStream,
                AutoCloseStream = false,
                ServerSideEncryptionMethod = _serverSideEncryptionMethod
            };

            Execute(client => client.PutObjectAsync(cacheRequest)).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Adds mirrored Cache From Physical.
        /// </summary>
        private void AddMirroredCacheFromPhysical(string path, string physicalPath)
        {
            var cacheRequest = new PutObjectRequest
            {
                BucketName = _bucketName,
                Key = BuildMirroredCacheKey(ResolveBucketPath(path)),
                CannedACL = _cannedACL,
                ContentType = _mimeTypeResolver.Resolve(path),
                FilePath = physicalPath,
                ServerSideEncryptionMethod = _serverSideEncryptionMethod
            };

            Execute(client => client.PutObjectAsync(cacheRequest)).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Deletes mirrored Cache Object By Source Path.
        /// </summary>
        private void DeleteMirroredCacheObjectBySourcePath(string sourcePath)
        {
            string cacheKey = BuildMirroredCacheKey(sourcePath);
            var request = new DeleteObjectRequest
            {
                BucketName = _bucketName,
                Key = cacheKey
            };

            Execute(client => client.DeleteObjectAsync(request)).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Builds mirrored Cache Key.
        /// </summary>
        private static string BuildMirroredCacheKey(string sourcePath)
        {
            string normalizedPath = sourcePath
                .Trim()
                .TrimStart(Delimiter.ToCharArray())
                .Replace("\\", Delimiter, StringComparison.Ordinal)
                .ToLowerInvariant();

            string normalizedPathWithoutMedia = TrimLeadingMediaSegment(normalizedPath);
            return $"cache/{normalizedPathWithoutMedia}";
        }

        /// <summary>
        /// Gets keys By Prefix.
        /// </summary>
        private IEnumerable<string> GetKeysByPrefix(string prefix)
        {
            if (string.IsNullOrWhiteSpace(prefix))
            {
                return [];
            }

            var listRequest = new ListObjectsRequest
            {
                BucketName = _bucketName,
                Prefix = prefix
            };

            return [.. ExecuteWithContinuation(listRequest)
                .SelectMany(p => p.S3Objects ?? Enumerable.Empty<S3Object>())
                .Select(p => p.Key)];
        }

        /// <summary>
        /// Deletes image Cache By Source Path.
        /// </summary>
        private void DeleteImageCacheBySourcePath(string sourcePath)
        {
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                return;
            }

            string normalizedPath = sourcePath
                .Trim()
                .TrimStart(Delimiter.ToCharArray())
                .Replace("\\", Delimiter, StringComparison.Ordinal)
                .ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(normalizedPath))
            {
                return;
            }

            string normalizedPathWithoutMedia = TrimLeadingMediaSegment(normalizedPath);
            string[] candidates = [.. new[] { normalizedPath, normalizedPathWithoutMedia }
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Distinct(StringComparer.Ordinal)];

            foreach (string candidate in candidates)
            {
                string escapedPath = Uri.EscapeDataString(candidate).Replace("%2F", Delimiter);
                DeleteObjectsByPrefix($"cache/{escapedPath}/");
                DeleteObjectsByPrefix($"cache/cache/{escapedPath}/");
            }
        }

        /// <summary>
        /// Removes a leading <c>media/</c> segment from a normalized path when present.
        /// </summary>
        /// <param name="normalizedPath">The normalized source path.</param>
        /// <returns>The path without the leading media segment.</returns>
        private static string TrimLeadingMediaSegment(string normalizedPath) =>
            normalizedPath.StartsWith("media/", StringComparison.Ordinal) ? normalizedPath["media/".Length..] : normalizedPath;

        /// <summary>
        /// Resolves bucket Path.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <param name="isDir">The isDir.</param>
        /// <returns>The result of the operation.</returns>
        public string ResolveBucketPath(string path, bool isDir = false)
        {
            if (string.IsNullOrEmpty(path))
                return _bucketPrefix;

            // Equalise delimiters
            path = path.Replace("/", Delimiter).Replace("\\", Delimiter);

            //Strip Root Path
            if (path.StartsWith(_rootPath, StringComparison.InvariantCultureIgnoreCase))
            {
                path = path[_rootPath.Length..];
                path = path.TrimStart(Delimiter.ToCharArray());
            }

            if (path.StartsWith(Delimiter))
                path = path[1..];

            //Remove Key Prefix If Duplicate
            if (path.StartsWith(_bucketPrefix, StringComparison.InvariantCultureIgnoreCase))
                path = path[_bucketPrefix.Length..];

            if (isDir && !path.EndsWith(Delimiter))
                path = string.Concat(path, Delimiter);

            if (path.StartsWith(Delimiter))
                path = path[1..];

            return string.Concat(_bucketPrefix, "/", WebUtility.UrlDecode(path));
        }

        /// <summary>
        /// Removes the configured S3 bucket prefix from an object key and normalizes delimiters.
        /// </summary>
        /// <param name="key">The full S3 key.</param>
        /// <returns>The normalized key without bucket prefix.</returns>
        protected virtual string RemovePrefix(string key)
        {
            if (!string.IsNullOrEmpty(_bucketPrefix) && key.StartsWith(_bucketPrefix))
                key = key[_bucketPrefix.Length..];

            return key.TrimStart(Delimiter.ToCharArray()).TrimEnd(Delimiter.ToCharArray());
        }


        /// <summary>
        /// Determines whether should Cache Image.
        /// </summary>
        private static bool ShouldCacheImage(string path, Stream inputStream)
        {
            var mimeType = TryDetectMimeType(inputStream);
            if (!string.IsNullOrWhiteSpace(mimeType))
            {
                return mimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
            }

            return IsImageExtension(path);
        }

        /// <summary>
        /// Attempts to detect Mime Type.
        /// </summary>
        private static string TryDetectMimeType(Stream inputStream)
        {
            if (!inputStream.CanSeek)
            {
                return null;
            }

            var originalPosition = inputStream.Position;
            try
            {
                var header = new byte[512];
                var bytesRead = inputStream.Read(header, 0, header.Length);

                if (bytesRead >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
                {
                    return "image/jpeg";
                }

                if (bytesRead >= 8
                    && header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47
                    && header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A)
                {
                    return "image/png";
                }

                if (bytesRead >= 6 && header[0] == 0x47 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x38)
                {
                    return "image/gif";
                }

                if (bytesRead >= 12
                    && header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46
                    && header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50)
                {
                    return "image/webp";
                }

                if (bytesRead >= 4 && header[0] == 0x00 && header[1] == 0x00 && header[2] == 0x01 && header[3] == 0x00)
                {
                    return "image/x-icon";
                }

                if (bytesRead >= 12
                    && header[4] == 0x66 && header[5] == 0x74 && header[6] == 0x79 && header[7] == 0x70
                    && ((header[8] == 0x61 && header[9] == 0x76 && header[10] == 0x69 && header[11] == 0x66)
                        || (header[8] == 0x61 && header[9] == 0x76 && header[10] == 0x69 && header[11] == 0x73)))
                {
                    return "image/avif";
                }

                if (bytesRead > 0)
                {
                    var sample = System.Text.Encoding.UTF8.GetString(header, 0, bytesRead);
                    if (sample.IndexOf("<svg", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return "image/svg+xml";
                    }
                }

                return null;
            }
            finally
            {
                inputStream.Position = originalPosition;
            }
        }

        /// <summary>
        /// Determines whether image extension.
        /// </summary>
        private static bool IsImageExtension(string path)
        {
            var extension = Path.GetExtension(path);
            if (string.IsNullOrWhiteSpace(extension))
            {
                return false;
            }

            return extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".png", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".gif", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".webp", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".svg", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".ico", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".avif", StringComparison.OrdinalIgnoreCase);
        }
        /// <summary>
        /// Handles the safe format operation.
        /// </summary>
        private static string SafeFormat(CultureInfo culture, string template, params object[] args)
        {
            if (string.IsNullOrEmpty(template))
            {
                return string.Empty;
            }

            try
            {
                return string.Format(culture, template, args);
            }
            catch (FormatException)
            {
                if (args == null || args.Length == 0)
                {
                    return template;
                }

                return template + " " + string.Join(", ", args);
            }
        }

        /// <summary>
        /// Resolves ui Culture.
        /// </summary>
        private CultureInfo ResolveUiCulture()
        {
            HttpContext context = _httpContextAccessor.HttpContext;
            if (context == null)
            {
                return CultureInfo.CurrentUICulture;
            }

            string[] headerCandidates = ["X-UI-Culture", "X-Ui-Culture", "X-UICulture", "X-Umbraco-Culture", "X-UMB-Culture"];
            foreach (string headerName in headerCandidates)
            {
                string headerValue = context.Request.Headers[headerName].ToString();
                if (!string.IsNullOrWhiteSpace(headerValue) && TryGetCulture(headerValue, out CultureInfo headerCulture))
                {
                    return NormalizeSupportedCulture(headerCulture);
                }
            }

            if (TryGetCultureFromClaims(context.User, out CultureInfo claimCulture))
            {
                return NormalizeSupportedCulture(claimCulture);
            }

            var backOfficeSecurityAccessor = context.RequestServices.GetService<IBackOfficeSecurityAccessor>();
            string backOfficeLanguage = backOfficeSecurityAccessor?.BackOfficeSecurity?.CurrentUser?.Language;
            if (!string.IsNullOrWhiteSpace(backOfficeLanguage) && TryGetCulture(backOfficeLanguage, out CultureInfo backOfficeCulture))
            {
                return NormalizeSupportedCulture(backOfficeCulture);
            }

            string acceptLanguage = context.Request.Headers.AcceptLanguage.ToString();
            if (!string.IsNullOrWhiteSpace(acceptLanguage))
            {
                foreach (string token in acceptLanguage.Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    string candidate = token.Split(';', StringSplitOptions.RemoveEmptyEntries)[0].Trim();
                    if (TryGetCulture(candidate, out CultureInfo parsedCulture))
                    {
                        return NormalizeSupportedCulture(parsedCulture);
                    }
                }
            }

            return CultureInfo.CurrentUICulture;
        }

        /// <summary>
        /// Handles the normalize supported culture operation.
        /// </summary>
        private static CultureInfo NormalizeSupportedCulture(CultureInfo culture)
        {
            if (culture.Name.Equals("it", StringComparison.OrdinalIgnoreCase))
            {
                return CultureInfo.GetCultureInfo("it-IT");
            }

            return culture;
        }

        /// <summary>
        /// Attempts to get Culture.
        /// </summary>
        private static bool TryGetCulture(string value, out CultureInfo culture)
        {
            try
            {
                culture = CultureInfo.GetCultureInfo(value);
                return true;
            }
            catch (CultureNotFoundException)
            {
                culture = CultureInfo.GetCultureInfo("en-US");
                return false;
            }
        }

        /// <summary>
        /// Attempts to get Culture From Claims.
        /// </summary>
        private static bool TryGetCultureFromClaims(ClaimsPrincipal user, out CultureInfo culture)
        {
            culture = CultureInfo.GetCultureInfo("en-US");
            if (user?.Identity?.IsAuthenticated != true)
            {
                return false;
            }

            foreach (Claim claim in user.Claims)
            {
                string type = claim.Type ?? string.Empty;
                if (!type.Contains("lang", StringComparison.OrdinalIgnoreCase) &&
                    !type.Contains("culture", StringComparison.OrdinalIgnoreCase) &&
                    !type.Contains("locale", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (TryGetCulture(claim.Value, out CultureInfo parsedCulture))
                {
                    culture = parsedCulture;
                    return true;
                }
            }

            return false;
        }
    }
}
