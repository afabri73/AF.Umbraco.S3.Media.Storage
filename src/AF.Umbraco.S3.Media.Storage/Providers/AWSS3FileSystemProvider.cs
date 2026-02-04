using Amazon.S3;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Concurrent;
using Umbraco.Cms.Core.Hosting;
using Umbraco.Extensions;

namespace AF.Umbraco.S3.Media.Storage.Providers
{

    /// <summary>
    /// Creates and caches named AWS S3 filesystem instances.
    /// </summary>
    class AWSS3FileSystemProvider : IAWSS3FileSystemProvider
    {
        /// <summary>
        /// Handles the new operation.
        /// </summary>
        private readonly ConcurrentDictionary<string, IAWSS3FileSystem> _fileSystems = new();
        /// <summary>
        /// Gets the s 3 client used by this component.
        /// </summary>
        private readonly IAmazonS3 _S3Client;
        /// <summary>
        /// Gets the options monitor used by this component.
        /// </summary>
        private readonly IOptionsMonitor<AWSS3FileSystemOptions> _optionsMonitor;
        /// <summary>
        /// Gets the hosting environment used by this component.
        /// </summary>
        private readonly IHostingEnvironment _hostingEnvironment;
        /// <summary>
        /// Gets the logger factory used by this component.
        /// </summary>
        private readonly ILoggerFactory _loggerFactory;
        /// <summary>
        /// Gets the file extension content type provider used by this component.
        /// </summary>
        private readonly FileExtensionContentTypeProvider _fileExtensionContentTypeProvider;
        /// <summary>
        /// Gets the mime type resolver used by this component.
        /// </summary>
        private readonly IMimeTypeResolver _mimeTypeResolver;
        /// <summary>
        /// Stores the HTTP context accessor.
        /// </summary>
        private readonly IHttpContextAccessor _httpContextAccessor;

        /// <summary>
        /// Initializes a new instance of the <see cref="AWSS3FileSystemProvider"/> class.
        /// </summary>
        /// <param name="optionsMonitor">The named filesystem options monitor.</param>
        /// <param name="hostingEnvironment">The hosting environment used for virtual path mapping.</param>
        /// <param name="loggerFactory">The logger factory.</param>
        /// <param name="mimeTypeResolver">The MIME type resolver.</param>
        /// <param name="s3Client">The AWS S3 client.</param>
        /// <param name="httpContextAccessor">Accessor used to resolve request culture data.</param>
        /// <exception cref="ArgumentNullException">Thrown when required dependencies are null.</exception>
        public AWSS3FileSystemProvider(IOptionsMonitor<AWSS3FileSystemOptions> optionsMonitor, IHostingEnvironment hostingEnvironment,
            ILoggerFactory loggerFactory, IMimeTypeResolver mimeTypeResolver, IAmazonS3 s3Client, IHttpContextAccessor httpContextAccessor)
        {
            _optionsMonitor = optionsMonitor ?? throw new ArgumentNullException(nameof(optionsMonitor));
            _hostingEnvironment = hostingEnvironment ?? throw new ArgumentNullException(nameof(hostingEnvironment));
            _loggerFactory = loggerFactory;
            _mimeTypeResolver = mimeTypeResolver;
            _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));

            _fileExtensionContentTypeProvider = new FileExtensionContentTypeProvider();

            _S3Client = s3Client;

            _optionsMonitor.OnChange(OptionsOnChange);
        }

        /// <summary>
        /// Gets file System.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <returns>The result of the operation.</returns>
        public IAWSS3FileSystem GetFileSystem(string name)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));

            return _fileSystems.GetOrAdd(name, CreateInstance);
        }

        /// <summary>
        /// Creates instance.
        /// </summary>
        private IAWSS3FileSystem CreateInstance(string name)
        {
            var options = _optionsMonitor.Get(name);

            return CreateInstance(options);
        }

        /// <summary>
        /// Creates instance.
        /// </summary>
        private IAWSS3FileSystem CreateInstance(AWSS3FileSystemOptions options)
        {
            return new AWSS3FileSystem(options, _hostingEnvironment, _fileExtensionContentTypeProvider,
                _loggerFactory.CreateLogger<AWSS3FileSystem>(), _mimeTypeResolver, _S3Client, _httpContextAccessor);
        }

        /// <summary>
        /// Applies updated options when configuration changes are detected.
        /// </summary>
        private void OptionsOnChange(AWSS3FileSystemOptions options, string name)
        {
            _fileSystems.TryUpdate(name, _ => CreateInstance(options));
        }
    }
}
