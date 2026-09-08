using SixLabors.ImageSharp.Web.Resolvers;
using SixLabors.ImageSharp.Web;
using System;
using System.IO;
using System.Threading.Tasks;

namespace AF.Umbraco.S3.Media.Storage.Resolvers
{

    /// <summary>
    /// Resolves media metadata and streams from the AWS S3-backed Umbraco filesystem.
    /// </summary>
    /// <param name="fileSystem">The media file system.</param>
    /// <param name="path">The media path.</param>
    /// <param name="metadata">The metadata retrieved while resolving the S3 object.</param>
    internal sealed class AWSS3MediaImageResolver(IAWSS3FileSystem fileSystem, string path, ImageMetadata metadata) : IImageResolver
    {
        /// <summary>
        /// Holds the media filesystem used to open content streams.
        /// </summary>
        private readonly IAWSS3FileSystem _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        /// <summary>
        /// Holds the media path resolved for this image request.
        /// </summary>
        private readonly string _path = path ?? throw new ArgumentNullException(nameof(path));
        /// <summary>
        /// Holds the metadata retrieved by the image provider so the resolver does not repeat S3 metadata requests.
        /// </summary>
        private readonly ImageMetadata _metadata = metadata;

        /// <summary>
        /// Gets the image metadata captured by the provider during object resolution.
        /// </summary>
        /// <returns>A task containing the image metadata.</returns>
        public Task<ImageMetadata> GetMetaDataAsync() => Task.FromResult(_metadata);

        /// <summary>
        /// Opens a readable stream for the requested media path.
        /// </summary>
        /// <returns>A task containing the readable media stream.</returns>
        public Task<Stream> OpenReadAsync() => Task.FromResult(_fileSystem.OpenFile(_path));
    }
}
