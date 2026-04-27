using System;
using System.Collections.Generic;
using System.IO;

namespace AF.Umbraco.S3.Media.Storage.Core
{
    /// <summary>
    /// Centralizes the formats that can be validated by the ImageSharp decoders bundled with the package.
    /// </summary>
    internal static class ImageSharpValidationFileTypes
    {
        private static readonly HashSet<string> ValidatedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".bmp",
            ".gif",
            ".jpeg",
            ".jpg",
            ".pbm",
            ".png",
            ".qoi",
            ".tga",
            ".tif",
            ".tiff",
            ".webp"
        };

        private static readonly HashSet<string> ValidatedContentTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "image/bmp",
            "image/gif",
            "image/jpeg",
            "image/pbm",
            "image/png",
            "image/qoi",
            "image/tga",
            "image/tif",
            "image/tiff",
            "image/webp",
            "image/x-portable-bitmap",
            "image/x-tga",
            "image/x-targa"
        };

        /// <summary>
        /// Determines whether a file should be validated with ImageSharp before it is stored.
        /// </summary>
        /// <param name="fileNameOrPath">File name or path used to resolve the extension.</param>
        /// <param name="contentType">Declared or resolved content type for the file.</param>
        /// <returns>
        /// <see langword="true" /> when the format is handled by the bundled ImageSharp decoders;
        /// otherwise <see langword="false" />, for example for SVG files.
        /// </returns>
        public static bool RequiresValidation(string fileNameOrPath, string contentType)
        {
            string extension = Path.GetExtension(fileNameOrPath);
            if (!string.IsNullOrWhiteSpace(extension))
            {
                return ValidatedExtensions.Contains(extension);
            }

            return !string.IsNullOrWhiteSpace(contentType)
                && ValidatedContentTypes.Contains(contentType);
        }
    }
}
