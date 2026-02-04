
namespace AF.Umbraco.S3.Media.Storage.Resolvers
{

    /// <summary>
    /// Resolves MIME types for media files handled by the package.
    /// </summary>
    public interface IMimeTypeResolver
    {
        /// <summary>
        /// Resolves the MIME type for the specified file name.
        /// </summary>
        /// <param name="filename">The file path or file name.</param>
        /// <returns>The resolved MIME type or a default fallback.</returns>
        string Resolve(string filename);
    }
}
