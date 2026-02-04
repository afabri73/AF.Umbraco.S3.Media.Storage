
namespace AF.Umbraco.S3.Media.Storage.Providers
{

    /// <summary>
    /// Provides access to named AWS S3 filesystem instances.
    /// </summary>
    public interface IAWSS3FileSystemProvider
    {
        /// <summary>
        /// Get the file system by its <paramref name="name" />.
        /// </summary>
        /// <param name="name">The name of the <see cref="IAWSS3FileSystem" />.</param>
        /// <returns>
        /// The <see cref="IAWSS3FileSystem" />.
        /// </returns>
        IAWSS3FileSystem GetFileSystem(string name);
    }
}
