using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp.Web;
using SixLabors.ImageSharp.Web.Providers;
using SixLabors.ImageSharp.Web.Resolvers;
using System;
using System.Net;
using System.Threading.Tasks;
using Umbraco.Cms.Core.Hosting;

namespace AF.Umbraco.S3.Media.Storage.Providers
{

    /// <summary>
    /// Resolves source media streams from S3 for ImageSharp processing.
    /// </summary>
    public class AWSS3FileSystemImageProvider : IImageProvider
    {
        /// <summary>
        /// Gets the named S3 filesystem configuration used by this provider.
        /// </summary>
        private readonly string _name;
        /// <summary>
        /// Gets the bucket name used by this component.
        /// </summary>
        private string _bucketName;
        /// <summary>
        /// Gets the file system provider used by this component.
        /// </summary>
        private readonly IAWSS3FileSystemProvider _fileSystemProvider;
        /// <summary>
        /// Gets the root path used by this component.
        /// </summary>
        private string _rootPath;
        /// <summary>
        /// Gets the format utilities used by this component.
        /// </summary>
        private readonly FormatUtilities _formatUtilities;
        /// <summary>
        /// Gets the s 3 client used by this component.
        /// </summary>
        private readonly IAmazonS3 _s3Client;

        /// <summary>
        /// A match function used by the resolver to identify itself as the correct resolver to use.
        /// </summary>
        private Func<HttpContext, bool> _match;

        /// <summary>
        /// Initializes a new instance of the <see cref="AWSS3FileSystemImageProvider" /> class.
        /// </summary>
        /// <param name="options">The options.</param>
        /// <param name="fileSystemProvider">The file system provider.</param>
        /// <param name="hostingEnvironment">The hosting environment.</param>
        /// <param name="formatUtilities">The format utilities.</param>
        /// <param name="s3Client">AWS S3 client.</param>
        public AWSS3FileSystemImageProvider(IOptionsMonitor<AWSS3FileSystemOptions> options, IAWSS3FileSystemProvider fileSystemProvider, IHostingEnvironment hostingEnvironment, FormatUtilities formatUtilities, IAmazonS3 s3Client)
            : this(AWSS3FileSystemOptions.MediaFileSystemName, options, fileSystemProvider, hostingEnvironment, formatUtilities, s3Client)
        { }

        /// <summary>
        /// Creates a new instance of <see cref="AWSS3FileSystemImageProvider" />.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="options">The options.</param>
        /// <param name="fileSystemProvider">The file system provider.</param>
        /// <param name="hostingEnvironment">The hosting environment.</param>
        /// <param name="formatUtilities">The format utilities.</param>
        /// <exception cref="System.ArgumentNullException">optionsFactory
        /// or.
        /// hostingEnvironment.
        /// or.
        /// name.
        /// or.
        /// fileSystemProvider.
        /// or.
        /// formatUtilities</exception>
        protected AWSS3FileSystemImageProvider(string name, IOptionsMonitor<AWSS3FileSystemOptions> options, IAWSS3FileSystemProvider fileSystemProvider, IHostingEnvironment hostingEnvironment, FormatUtilities formatUtilities, IAmazonS3 s3Client)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (hostingEnvironment == null) throw new ArgumentNullException(nameof(hostingEnvironment));

            _name = name ?? throw new ArgumentNullException(nameof(name));
            _fileSystemProvider = fileSystemProvider ?? throw new ArgumentNullException(nameof(fileSystemProvider));
            _formatUtilities = formatUtilities ?? throw new ArgumentNullException(nameof(formatUtilities));

            var fileSystemOptions = options.Get(name);
            _rootPath = hostingEnvironment.ToAbsolute(fileSystemOptions.VirtualPath);
            _bucketName = fileSystemOptions.BucketName;

            _s3Client = s3Client;

            options.OnChange((o, n) => OptionsOnChange(o, n, hostingEnvironment));
        }

        /// <summary>
        /// Determines whether the request targets a supported image extension.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <returns><see langword="true" /> when the request contains a supported extension; otherwise, <see langword="false" />.</returns>
        public bool IsValidRequest(HttpContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            return _formatUtilities.TryGetExtensionFromUri(context.Request.GetDisplayUrl(), out _);
        }

        /// <summary>
        /// Resolves an existing S3 object for ImageSharp processing.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <returns>A resolver for an existing object, or <see langword="null" /> when S3 returns HTTP 404.</returns>
        /// <remarks>Authorization failures are propagated so IAM configuration errors remain observable.</remarks>
        public Task<IImageResolver> GetAsync(HttpContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            return GetResolverAsync(context);
        }

        /// <summary>
        /// Performs the S3 metadata lookup and creates a resolver that reuses the response.
        /// </summary>
        private async Task<IImageResolver> GetResolverAsync(HttpContext context)
        {
            IAWSS3FileSystem fileSystem = _fileSystemProvider.GetFileSystem(_name);
            string path = context.Request.Path.Value ?? string.Empty;
            var request = new GetObjectMetadataRequest
            {
                BucketName = _bucketName,
                Key = fileSystem.ResolveBucketPath(path)
            };

            try
            {
                GetObjectMetadataResponse response = await _s3Client
                    .GetObjectMetadataAsync(request, context.RequestAborted)
                    .ConfigureAwait(false);
                var metadata = new ImageMetadata(
                    new DateTimeOffset(response.LastModified ?? DateTime.UtcNow).UtcDateTime,
                    response.ContentLength);

                return new AWSS3MediaImageResolver(fileSystem, path, metadata);
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }
        }

        /// <summary>
        /// Gets the ProcessingBehavior value.
        /// </summary>
        public ProcessingBehavior ProcessingBehavior => ProcessingBehavior.CommandOnly;

        /// <summary>
        /// Gets or sets the request match function used to determine whether this provider should handle a request.
        /// </summary>
        public Func<HttpContext, bool> Match
        {
            get => this._match ?? IsMatch;
            set => this._match = value;
        }

        /// <summary>
        /// Determines whether match.
        /// </summary>
        private bool IsMatch(HttpContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            return context.Request.Path.StartsWithSegments(_rootPath, StringComparison.InvariantCultureIgnoreCase);
        }

        /// <summary>
        /// Applies updated options when configuration changes are detected.
        /// </summary>
        private void OptionsOnChange(AWSS3FileSystemOptions options, string name, IHostingEnvironment hostingEnvironment)
        {
            if (!string.Equals(name, _name, StringComparison.Ordinal)) return;

            _rootPath = hostingEnvironment.ToAbsolute(options.VirtualPath);
            _bucketName = options.BucketName;
        }
    }
}
