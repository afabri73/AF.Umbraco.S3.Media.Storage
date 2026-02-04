using SixLabors.ImageSharp.Web.Caching;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace AF.Umbraco.S3.Media.Storage.Core
{

    /// <summary>
    /// Creates deterministic cache hashes while preserving media-folder scoping in generated keys.
    /// </summary>
    public class AWSS3ScopedCacheHash : ICacheHash
    {
        /// <summary>
        /// Creates a scoped hash path for the provided value.
        /// </summary>
        /// <param name="value">The source value used as cache key input.</param>
        /// <param name="length">The desired hash length.</param>
        /// <returns>A scoped cache path containing the hashed file name.</returns>
        public string Create(string value, uint length)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "unknown/empty.img";
            }

            string pathPart = value;
            int queryIndex = value.IndexOf('?', StringComparison.Ordinal);
            if (queryIndex >= 0)
            {
                pathPart = value[..queryIndex];
            }

            pathPart = pathPart.Trim().TrimStart('/').Replace("\\", "/", StringComparison.Ordinal).ToLowerInvariant();
            // Keep cache structure scoped by media folders but without the leading "media/" segment.
            if (pathPart.StartsWith("media/", StringComparison.Ordinal))
            {
                pathPart = pathPart["media/".Length..];
            }
            if (string.IsNullOrWhiteSpace(pathPart))
            {
                pathPart = "unknown";
            }

            string escapedPath = Uri.EscapeDataString(pathPart).Replace("%2F", "/");
            string extension = Path.GetExtension(pathPart);
            if (string.IsNullOrWhiteSpace(extension))
            {
                extension = ".img";
            }

            string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
            int safeLength = (int)Math.Max(8, Math.Min(length, (uint)hash.Length));

            return $"{escapedPath}/{hash[..safeLength]}{extension}";
        }
    }
}
