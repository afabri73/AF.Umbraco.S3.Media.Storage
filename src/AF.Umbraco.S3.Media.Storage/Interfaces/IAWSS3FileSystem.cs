using Amazon.S3;
using Umbraco.Cms.Core.IO;

namespace AF.Umbraco.S3.Media.Storage.Interfaces
{

    /// <summary>
    /// Extends Umbraco's filesystem abstraction with AWS S3-specific operations.
    /// </summary>
    public interface IAWSS3FileSystem : IFileSystem
    {
        /// <summary>
        /// Gets the active AWS S3 client used by this file system.
        /// </summary>
        /// <param name="path">The relative path to the blob.</param>
        /// <returns>The S3 client instance.</returns>
        IAmazonS3 GetS3Client(string path);

        /// <summary>
        /// Resolves an Umbraco media path into an S3 object key or key prefix.
        /// </summary>
        /// <param name="path">The input path.</param>
        /// <param name="isDir">Whether the path should be normalized as a directory prefix.</param>
        /// <returns>The resolved S3 key.</returns>
        string ResolveBucketPath(string path, bool isDir = false);
    }
}
