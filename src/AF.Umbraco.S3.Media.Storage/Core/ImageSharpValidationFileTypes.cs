using System;
using System.Collections.Generic;
using System.IO;

namespace AF.Umbraco.S3.Media.Storage.Core
{

    /// <summary>
    /// Centralizes the file types that can be validated with the ImageSharp decoders bundled by this package.
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
