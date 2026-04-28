using AF.Umbraco.S3.Media.Storage.Core;
using AF.Umbraco.S3.Media.Storage.Options;
using AF.Umbraco.S3.Media.Storage.Resolvers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Logging.Abstractions;
using System.Reflection;
using Umbraco.Cms.Core.Hosting;
using Xunit;

namespace AF.Umbraco.S3.Media.Storage.Tests;

/// <summary>
/// Verifies that S3 bucket prefixes stay separate from public Umbraco media URLs.
/// </summary>
public sealed class AWSS3FileSystemBucketPrefixTests
{
    [Theory]
    [InlineData("", "media")]
    [InlineData("   ", "media")]
    [InlineData("/Tenant-A/Media/", "tenant-a/media")]
    [InlineData(@"Tenant-A\Media", "tenant-a/media")]
    public void MediaBucketPrefix_NormalizesConfiguredValue(string configuredPrefix, string expectedPrefix)
    {
        var options = new AWSS3FileSystemOptions { MediaBucketPrefix = configuredPrefix };

        Assert.Equal(expectedPrefix, options.MediaBucketPrefix);
    }

    [Theory]
    [InlineData("", "cache")]
    [InlineData("   ", "cache")]
    [InlineData("/Tenant-A/Cache/", "tenant-a/cache")]
    [InlineData(@"Tenant-A\Cache", "tenant-a/cache")]
    public void CacheBucketPrefix_NormalizesConfiguredValue(string configuredPrefix, string expectedPrefix)
    {
        var options = new AWSS3FileSystemOptions { CacheBucketPrefix = configuredPrefix };

        Assert.Equal(expectedPrefix, options.CacheBucketPrefix);
    }

    [Fact]
    public void GetUrl_CustomMediaBucketPrefixWithoutHost_UsesPublicMediaPath()
    {
        AWSS3FileSystem fileSystem = CreateFileSystem(mediaBucketPrefix: "tenant-a/media");

        Assert.Equal("tenant-a/media/example.jpg", fileSystem.ResolveBucketPath("/media/example.jpg"));
        Assert.Equal("/media/example.jpg", fileSystem.GetUrl("/media/example.jpg"));
    }

    [Fact]
    public void GetUrl_CustomMediaBucketPrefixWithHost_UsesBucketPathBehindHost()
    {
        AWSS3FileSystem fileSystem = CreateFileSystem(
            mediaBucketPrefix: "tenant-a/media",
            bucketHostName: "https://cdn.example.com/");

        Assert.Equal("https://cdn.example.com/tenant-a/media/example.jpg", fileSystem.GetUrl("/media/example.jpg"));
    }

    [Fact]
    public void ResolveBucketPath_DoesNotTreatSiblingPrefixAsDuplicate()
    {
        AWSS3FileSystem fileSystem = CreateFileSystem(mediaBucketPrefix: "media");

        Assert.Equal("media/media-assets/example.jpg", fileSystem.ResolveBucketPath("media-assets/example.jpg"));
        Assert.Equal("media/media-assets/example.jpg", fileSystem.ResolveBucketPath("/media-assets/example.jpg"));
    }

    [Fact]
    public void BuildMirroredCacheKey_CustomPrefixes_UsesConfiguredCacheBucketPrefix()
    {
        AWSS3FileSystem fileSystem = CreateFileSystem(
            mediaBucketPrefix: "tenant-a/media",
            cacheBucketPrefix: "tenant-a/cache");

        MethodInfo method = typeof(AWSS3FileSystem).GetMethod("BuildMirroredCacheKey", BindingFlags.Instance | BindingFlags.NonPublic)!;
        string cacheKey = (string)method.Invoke(fileSystem, ["tenant-a/media/example.jpg"])!;

        Assert.Equal("tenant-a/cache/example.jpg", cacheKey);
    }

    private static AWSS3FileSystem CreateFileSystem(string mediaBucketPrefix, string cacheBucketPrefix = "cache", string bucketHostName = "")
    {
        var options = new AWSS3FileSystemOptions
        {
            BucketName = "test-bucket",
            VirtualPath = "~/media",
            MediaBucketPrefix = mediaBucketPrefix,
            CacheBucketPrefix = cacheBucketPrefix,
            BucketHostName = bucketHostName
        };

        return new AWSS3FileSystem(
            options,
            new TestHostingEnvironment(),
            new FileExtensionContentTypeProvider(),
            NullLogger<AWSS3FileSystem>.Instance,
            new TestMimeTypeResolver(),
            null!,
            new HttpContextAccessor());
    }

    private sealed class TestMimeTypeResolver : IMimeTypeResolver
    {
        public string Resolve(string path) => "application/octet-stream";
    }

    private sealed class TestHostingEnvironment : IHostingEnvironment
    {
        public string ApplicationId => "test";

        public string ApplicationPhysicalPath => "/";

        public string ApplicationVirtualPath => "/";

        public string SiteName => "test";

        public string LocalTempPath => "/tmp";

        public bool IsHosted => true;

        public bool IsDebugMode => false;

        public Uri ApplicationMainUrl => new("https://example.com/");

        public string MapPathWebRoot(string path) => path;

        public string MapPathContentRoot(string path) => path;

        public string ToAbsolute(string virtualPath)
        {
            if (virtualPath.StartsWith("~/", StringComparison.Ordinal))
            {
                return "/" + virtualPath[2..];
            }

            return virtualPath;
        }

        public void EnsureApplicationMainUrl(Uri? currentApplicationUrl)
        {
        }
    }
}
