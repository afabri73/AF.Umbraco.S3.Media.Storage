using Microsoft.AspNetCore.Http;
using SixLabors.ImageSharp.Web.Caching;
using SixLabors.ImageSharp.Web.Commands;
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace AF.Umbraco.S3.Media.Storage.Core
{

    /// <summary>
    /// Creates cache keys scoped by source media path and image command set.
    /// </summary>
    public class AWSS3ScopedCacheKey : ICacheKey
    {
        /// <summary>
        /// Creates a cache key from request path and ImageSharp commands.
        /// </summary>
        /// <param name="context">The current HTTP context.</param>
        /// <param name="commands">The image processing commands.</param>
        /// <returns>A scoped cache key including a deterministic hash suffix.</returns>
        public string Create(HttpContext context, CommandCollection commands)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            string sourcePath = (context.Request.Path.Value ?? string.Empty).TrimStart('/').ToLowerInvariant();
            if (sourcePath.StartsWith("media/", StringComparison.Ordinal))
            {
                sourcePath = sourcePath["media/".Length..];
            }
            string escapedSourcePath = Uri.EscapeDataString(sourcePath).Replace("%2F", "/");

            string commandString = string.Join("&", commands
                .OrderBy(c => c.Key, StringComparer.Ordinal)
                .Select(c => $"{c.Key}={c.Value}"));

            string hashInput = $"{sourcePath}?{commandString}";
            string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(hashInput))).ToLowerInvariant();

            string extension = Path.GetExtension(sourcePath);
            if (string.IsNullOrWhiteSpace(extension))
            {
                extension = ".img";
            }

            return $"{escapedSourcePath}/{hash}{extension}";
        }
    }
}
