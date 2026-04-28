using Amazon.S3;
using System;
using System.ComponentModel.DataAnnotations;

namespace AF.Umbraco.S3.Media.Storage.Options
{

    /// <summary>
    /// Defines configuration settings for an AWS S3-backed Umbraco filesystem.
    /// </summary>
    public class AWSS3FileSystemOptions
    {
        public const string MediaFileSystemName = "Media";

        public const string DefaultMediaBucketPrefix = "media";
        public const string DefaultCacheBucketPrefix = "cache";

        private string _mediaBucketPrefix = DefaultMediaBucketPrefix;
        private string _cacheBucketPrefix = DefaultCacheBucketPrefix;

        public string MediaBucketPrefix
        {
            get => _mediaBucketPrefix;
            set => _mediaBucketPrefix = NormalizeBucketPrefix(value, DefaultMediaBucketPrefix);
        }

        public string CacheBucketPrefix
        {
            get => _cacheBucketPrefix;
            set => _cacheBucketPrefix = NormalizeBucketPrefix(value, DefaultCacheBucketPrefix);
        }

        public string Region { get; set; } = null!;

        [Required]
        public string BucketName { get; set; } = null!;

        [Required]
        public string VirtualPath { get; set; } = null!;

        public string BucketHostName { get; set; } = null!;

        public S3CannedACL CannedACL { get; set; }

        public ServerSideEncryptionMethod ServerSideEncryptionMethod { get; set; }

        public AWSS3CacheRetentionOptions CacheRetention { get; set; } = new();

        public static string NormalizeBucketPrefix(string value, string defaultPrefix)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return defaultPrefix;
            }

            string normalized = value.Trim().Replace("\\", "/", StringComparison.Ordinal);
            normalized = string.Join("/", normalized.Split('/', StringSplitOptions.RemoveEmptyEntries));

            return string.IsNullOrWhiteSpace(normalized)
                ? defaultPrefix
                : normalized.ToLowerInvariant();
        }
    }

    /// <summary>
    /// Options that control lifecycle cleanup behavior for the S3 thumbnail cache.
    /// </summary>
    public class AWSS3CacheRetentionOptions
    {
        public bool Enabled { get; set; } = true;

        [Range(1, 3650)]
        public int NumberOfDays { get; set; } = 90;

        public bool TestModeEnable { get; set; }

        [Range(5, 86400)]
        public int TestModeSweepSeconds { get; set; } = 30;

        [Range(1, 525600)]
        public int TestModeMaxAgeMinutes { get; set; } = 10;
    }
}
