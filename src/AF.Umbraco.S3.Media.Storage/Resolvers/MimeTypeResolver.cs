using Microsoft.AspNetCore.StaticFiles;

namespace AF.Umbraco.S3.Media.Storage.Resolvers
{

    /// <summary>
    /// Resolves MIME types using ASP.NET Core's file extension content type provider.
    /// </summary>
    class MimeTypeResolver : IMimeTypeResolver
    {
        /// <summary>
        /// Gets the name used by this component.
        /// </summary>
        /// <param name="filename">The filename.</param>
        /// <returns>The result of the operation.</returns>
        public string Resolve(string filename)
        {
            new FileExtensionContentTypeProvider().TryGetContentType(filename, out string contentType);
            return contentType ?? "application/octet-stream";
        }
    }
}
