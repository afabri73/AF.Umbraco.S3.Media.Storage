using AF.Umbraco.S3.Media.Storage.Interfaces;
using AF.Umbraco.S3.Media.Storage.Middlewares;
using AF.Umbraco.S3.Media.Storage.Options;
using AF.Umbraco.S3.Media.Storage.Providers;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp.Web;
using SixLabors.ImageSharp.Web.Commands;
using SixLabors.ImageSharp.Web.Middleware;
using SixLabors.ImageSharp.Web.Processors;
using System.Net;
using Umbraco.Cms.Core.Hosting;
using Xunit;

namespace AF.Umbraco.S3.Media.Storage.Tests;

/// <summary>
/// Verifies that S3 media delivery does not prevent ImageSharp from handling processing commands.
/// </summary>
public sealed class AWSS3FileSystemMiddlewareImageSharpTests
{
    /// <summary>
    /// Ensures a media request carrying an ImageSharp command continues through the pipeline before S3 is accessed.
    /// </summary>
    [Fact]
    public async Task InvokeAsync_ImageSharpCommand_ContinuesToNextMiddleware()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/media/example.jpg";
        context.Request.QueryString = new QueryString("?width=320&v=1dcce71ae06a690");
        bool nextInvoked = false;

        using var s3Client = new TestAmazonS3Client();
        AWSS3FileSystemMiddleware middleware = CreateMiddleware(s3Client);

        await middleware.InvokeAsync(context, _ =>
        {
            nextInvoked = true;
            return Task.CompletedTask;
        });

        Assert.True(nextInvoked);
        Assert.Equal(0, s3Client.MetadataRequestCount);
    }

    /// <summary>
    /// Ensures an unrelated query parameter does not make the middleware bypass the original S3 media file.
    /// </summary>
    [Fact]
    public async Task InvokeAsync_UnknownQueryParameter_ChecksS3BeforeContinuing()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/media/example.png";
        context.Request.QueryString = new QueryString("?v=1dcce71ae06a690");
        bool nextInvoked = false;

        using var s3Client = new TestAmazonS3Client();
        AWSS3FileSystemMiddleware middleware = CreateMiddleware(s3Client);

        await middleware.InvokeAsync(context, _ =>
        {
            nextInvoked = true;
            return Task.CompletedTask;
        });

        Assert.Equal(1, s3Client.MetadataRequestCount);
        Assert.True(nextInvoked);
    }

    /// <summary>
    /// Ensures requests outside the configured media path continue through the pipeline unchanged.
    /// </summary>
    [Fact]
    public async Task InvokeAsync_NonMediaPath_ContinuesToNextMiddleware()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/health";
        bool nextInvoked = false;

        using var s3Client = new TestAmazonS3Client();
        AWSS3FileSystemMiddleware middleware = CreateMiddleware(s3Client);

        await middleware.InvokeAsync(context, _ =>
        {
            nextInvoked = true;
            return Task.CompletedTask;
        });

        Assert.True(nextInvoked);
        Assert.Equal(0, s3Client.MetadataRequestCount);
    }

    private static AWSS3FileSystemMiddleware CreateMiddleware(IAmazonS3 s3Client)
    {
        var requestParser = new QueryCollectionRequestParser();
        var requestAuthorizationUtilities = new RequestAuthorizationUtilities(
            Microsoft.Extensions.Options.Options.Create(new ImageSharpMiddlewareOptions()),
            requestParser,
            [new ResizeWebProcessor()],
            new CommandParser([]),
            new ServiceCollection().BuildServiceProvider());

        return new(
            new StaticOptionsMonitor(new AWSS3FileSystemOptions
            {
                BucketName = "test-bucket",
                VirtualPath = "~/media"
            }),
            new TestFileSystemProvider(),
            new TestHostingEnvironment(),
            s3Client,
            requestParser,
            requestAuthorizationUtilities);
    }

    private sealed class StaticOptionsMonitor(AWSS3FileSystemOptions options) : IOptionsMonitor<AWSS3FileSystemOptions>
    {
        public AWSS3FileSystemOptions CurrentValue => options;

        public AWSS3FileSystemOptions Get(string? name) => options;

        public IDisposable? OnChange(Action<AWSS3FileSystemOptions, string?> listener) => null;
    }

    private sealed class TestAmazonS3Client : AmazonS3Client
    {
        public TestAmazonS3Client()
            : base(new AnonymousAWSCredentials(), RegionEndpoint.USEast1)
        {
        }

        public int MetadataRequestCount { get; private set; }

        public override Task<GetObjectMetadataResponse> GetObjectMetadataAsync(GetObjectMetadataRequest request, CancellationToken cancellationToken = default)
        {
            MetadataRequestCount++;

            return Task.FromException<GetObjectMetadataResponse>(new AmazonS3Exception("Not found")
            {
                StatusCode = HttpStatusCode.NotFound
            });
        }
    }

    private sealed class TestFileSystemProvider : IAWSS3FileSystemProvider
    {
        public IAWSS3FileSystem GetFileSystem(string name) => null!;
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

        public string ToAbsolute(string virtualPath) => virtualPath.StartsWith("~/", StringComparison.Ordinal)
            ? "/" + virtualPath[2..]
            : virtualPath;

        public void EnsureApplicationMainUrl(Uri? currentApplicationUrl)
        {
        }
    }
}
