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
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp.Web;
using SixLabors.ImageSharp.Web.Commands;
using SixLabors.ImageSharp.Web.DependencyInjection;
using SixLabors.ImageSharp.Web.Middleware;
using SixLabors.ImageSharp.Web.Processors;
using System.Globalization;
using System.Net;
using Umbraco.Cms.Core.Hosting;
using Xunit;

namespace AF.Umbraco.S3.Media.Storage.Tests;

/// <summary>
/// Verifies that S3 media delivery routes only recognized commands to ImageSharp.
/// </summary>
public sealed class AWSS3FileSystemMiddlewareImageSharpTests
{
    private static readonly byte[] PngContent = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>
    /// Verifies that a recognized ImageSharp command combined with unrelated parameters continues through the pipeline without accessing S3.
    /// </summary>
    [Fact]
    public async Task InvokeAsync_ImageSharpCommand_ContinuesToNextMiddleware()
    {
        using ServiceProvider imageSharpServices = CreateImageSharpServices();
        using var s3Client = new TestAmazonS3Client();
        AWSS3FileSystemMiddleware middleware = CreateMiddleware(s3Client, imageSharpServices);
        DefaultHttpContext context = CreateMediaContext("?width=320&v=1dcce71ae06a690");
        bool nextInvoked = false;

        await middleware.InvokeAsync(context, _ =>
        {
            nextInvoked = true;
            return Task.CompletedTask;
        });

        Assert.True(nextInvoked);
        Assert.Equal(0, s3Client.MetadataRequestCount);
    }

    /// <summary>
    /// Verifies that an unrelated parameter does not prevent complete delivery of the original media from S3.
    /// </summary>
    [Fact]
    public async Task InvokeAsync_UnknownQueryParameter_ServesOriginalMediaFromS3()
    {
        using ServiceProvider imageSharpServices = CreateImageSharpServices();
        using var s3Client = new TestAmazonS3Client(PngContent, "image/png");
        AWSS3FileSystemMiddleware middleware = CreateMiddleware(s3Client, imageSharpServices);
        DefaultHttpContext context = CreateMediaContext("?v=1dcce71ae06a690");
        bool nextInvoked = false;

        await middleware.InvokeAsync(context, _ =>
        {
            nextInvoked = true;
            return Task.CompletedTask;
        });

        Assert.False(nextInvoked);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Equal("image/png", context.Response.ContentType);
        Assert.Equal(PngContent.Length, context.Response.ContentLength);
        Assert.Equal(PngContent, ((MemoryStream)context.Response.Body).ToArray());
        Assert.Equal(1, s3Client.MetadataRequestCount);
        Assert.Equal(1, s3Client.ObjectRequestCount);
        Assert.Equal("media/example.png", s3Client.LastMetadataRequestKey);
        Assert.Equal("media/example.png", s3Client.LastObjectRequestKey);
    }

    /// <summary>
    /// Verifies that the legacy public constructor resolves ImageSharp filtering from request services.
    /// </summary>
    [Fact]
    public async Task InvokeAsync_LegacyPublicConstructor_UsesRequestServicesFiltering()
    {
        using ServiceProvider imageSharpServices = CreateImageSharpServices();
        using var s3Client = new TestAmazonS3Client();
        AWSS3FileSystemMiddleware middleware = CreateLegacyMiddleware(s3Client, imageSharpServices);
        DefaultHttpContext context = CreateMediaContext("?v=1dcce71ae06a690");
        context.RequestServices = imageSharpServices;
        bool nextInvoked = false;

        await middleware.InvokeAsync(context, _ =>
        {
            nextInvoked = true;
            return Task.CompletedTask;
        });

        Assert.Equal(1, s3Client.MetadataRequestCount);
        Assert.True(nextInvoked);
    }

    /// <summary>
    /// Verifies that the legacy protected constructor applies the same filtering as the public path.
    /// </summary>
    [Fact]
    public async Task InvokeAsync_LegacyProtectedConstructor_UsesRequestServicesFiltering()
    {
        using ServiceProvider imageSharpServices = CreateImageSharpServices();
        using var s3Client = new TestAmazonS3Client();
        var middleware = new LegacyProtectedMiddleware(
            CreateOptionsMonitor(),
            new TestFileSystemProvider(),
            new TestHostingEnvironment(),
            s3Client,
            imageSharpServices.GetRequiredService<IRequestParser>());
        DefaultHttpContext context = CreateMediaContext("?v=1dcce71ae06a690");
        context.RequestServices = imageSharpServices;
        bool nextInvoked = false;

        await middleware.InvokeAsync(context, _ =>
        {
            nextInvoked = true;
            return Task.CompletedTask;
        });

        Assert.Equal(1, s3Client.MetadataRequestCount);
        Assert.True(nextInvoked);
    }

    /// <summary>
    /// Verifies that the legacy constructor preserves previous behavior when the request does not expose dependency-injection services.
    /// </summary>
    [Fact]
    public async Task InvokeAsync_LegacyPublicConstructorWithoutRequestServices_PreservesPreviousRouting()
    {
        using ServiceProvider imageSharpServices = CreateImageSharpServices();
        using var s3Client = new TestAmazonS3Client();
        AWSS3FileSystemMiddleware middleware = CreateLegacyMiddleware(s3Client, imageSharpServices);
        DefaultHttpContext context = CreateMediaContext("?v=1dcce71ae06a690");
        bool nextInvoked = false;

        await middleware.InvokeAsync(context, _ =>
        {
            nextInvoked = true;
            return Task.CompletedTask;
        });

        Assert.True(nextInvoked);
        Assert.Equal(0, s3Client.MetadataRequestCount);
    }

    /// <summary>
    /// Verifies that standard dependency-injection activation selects the constructor with injected ImageSharp filtering.
    /// </summary>
    [Fact]
    public async Task InvokeAsync_DependencyInjection_InjectsRequestFiltering()
    {
        using var s3Client = new TestAmazonS3Client();
        var services = new ServiceCollection();
        services.AddImageSharp();
        services.AddSingleton<IOptionsMonitor<AWSS3FileSystemOptions>>(CreateOptionsMonitor());
        services.AddSingleton<IAWSS3FileSystemProvider, TestFileSystemProvider>();
        services.AddSingleton<IHostingEnvironment, TestHostingEnvironment>();
        services.AddSingleton<IAmazonS3>(s3Client);

        using ServiceProvider serviceProvider = services.BuildServiceProvider();
        using ServiceProvider emptyRequestServices = new ServiceCollection().BuildServiceProvider();
        AWSS3FileSystemMiddleware middleware = ActivatorUtilities.CreateInstance<AWSS3FileSystemMiddleware>(serviceProvider);
        DefaultHttpContext context = CreateMediaContext("?v=1dcce71ae06a690");
        context.RequestServices = emptyRequestServices;
        bool nextInvoked = false;

        await middleware.InvokeAsync(context, _ =>
        {
            nextInvoked = true;
            return Task.CompletedTask;
        });

        Assert.Equal(1, s3Client.MetadataRequestCount);
        Assert.True(nextInvoked);
    }

    /// <summary>
    /// Verifies that commands declared by custom processors are routed to ImageSharp.
    /// </summary>
    [Fact]
    public async Task InvokeAsync_CustomProcessorCommand_ContinuesToNextMiddleware()
    {
        using ServiceProvider imageSharpServices = CreateImageSharpServices(includeCustomProcessor: true);
        using var s3Client = new TestAmazonS3Client();
        AWSS3FileSystemMiddleware middleware = CreateMiddleware(s3Client, imageSharpServices);
        DefaultHttpContext context = CreateMediaContext("?custom=enabled&v=1dcce71ae06a690");
        bool nextInvoked = false;

        await middleware.InvokeAsync(context, _ =>
        {
            nextInvoked = true;
            return Task.CompletedTask;
        });

        Assert.True(nextInvoked);
        Assert.Equal(0, s3Client.MetadataRequestCount);
    }

    /// <summary>
    /// Verifies that ImageSharp remains solely responsible for the parsing callback and that it is not invoked twice.
    /// </summary>
    [Fact]
    public async Task InvokeAsync_UnknownQueryParameter_DoesNotInvokeImageSharpParseCallback()
    {
        int callbackInvocations = 0;
        using ServiceProvider imageSharpServices = CreateImageSharpServices(
            options => options.OnParseCommandsAsync = _ =>
            {
                callbackInvocations++;
                return Task.CompletedTask;
            });
        using var s3Client = new TestAmazonS3Client();
        AWSS3FileSystemMiddleware middleware = CreateMiddleware(s3Client, imageSharpServices);
        DefaultHttpContext context = CreateMediaContext("?v=1dcce71ae06a690");

        await middleware.InvokeAsync(context, _ => Task.CompletedTask);

        Assert.Equal(0, callbackInvocations);
        Assert.Equal(1, s3Client.MetadataRequestCount);
    }

    /// <summary>
    /// Verifies that requests outside the media path continue unchanged without accessing S3.
    /// </summary>
    [Fact]
    public async Task InvokeAsync_NonMediaPath_ContinuesToNextMiddleware()
    {
        using ServiceProvider imageSharpServices = CreateImageSharpServices();
        using var s3Client = new TestAmazonS3Client();
        AWSS3FileSystemMiddleware middleware = CreateMiddleware(s3Client, imageSharpServices);
        var context = new DefaultHttpContext();
        context.Request.Path = "/health";
        bool nextInvoked = false;

        await middleware.InvokeAsync(context, _ =>
        {
            nextInvoked = true;
            return Task.CompletedTask;
        });

        Assert.True(nextInvoked);
        Assert.Equal(0, s3Client.MetadataRequestCount);
    }

    private static DefaultHttpContext CreateMediaContext(string queryString)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/media/example.png";
        context.Request.QueryString = new QueryString(queryString);
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static ServiceProvider CreateImageSharpServices(
        Action<ImageSharpMiddlewareOptions>? configure = null,
        bool includeCustomProcessor = false)
    {
        var services = new ServiceCollection();
        IImageSharpBuilder builder = services.AddImageSharp(configure ?? (_ => { }));

        if (includeCustomProcessor)
        {
            builder.AddProcessor<CustomImageWebProcessor>();
        }

        return services.BuildServiceProvider();
    }

    private static AWSS3FileSystemMiddleware CreateMiddleware(IAmazonS3 s3Client, IServiceProvider imageSharpServices) =>
        new(
            CreateOptionsMonitor(),
            new TestFileSystemProvider(),
            new TestHostingEnvironment(),
            s3Client,
            imageSharpServices.GetRequiredService<IRequestParser>(),
            imageSharpServices.GetRequiredService<RequestAuthorizationUtilities>());

    private static AWSS3FileSystemMiddleware CreateLegacyMiddleware(IAmazonS3 s3Client, IServiceProvider imageSharpServices) =>
        new(
            CreateOptionsMonitor(),
            new TestFileSystemProvider(),
            new TestHostingEnvironment(),
            s3Client,
            imageSharpServices.GetRequiredService<IRequestParser>());

    private static StaticOptionsMonitor CreateOptionsMonitor() =>
        new(new AWSS3FileSystemOptions
        {
            BucketName = "test-bucket",
            VirtualPath = "~/media"
        });

    private sealed class StaticOptionsMonitor(AWSS3FileSystemOptions options) : IOptionsMonitor<AWSS3FileSystemOptions>
    {
        public AWSS3FileSystemOptions CurrentValue => options;

        public AWSS3FileSystemOptions Get(string? name) => options;

        public IDisposable? OnChange(Action<AWSS3FileSystemOptions, string?> listener) => null;
    }

    private sealed class TestAmazonS3Client(byte[]? content = null, string contentType = "application/octet-stream") : AmazonS3Client(
        new AnonymousAWSCredentials(),
        RegionEndpoint.USEast1)
    {
        public int MetadataRequestCount { get; private set; }

        public int ObjectRequestCount { get; private set; }

        public string? LastMetadataRequestKey { get; private set; }

        public string? LastObjectRequestKey { get; private set; }

        public override Task<GetObjectMetadataResponse> GetObjectMetadataAsync(
            GetObjectMetadataRequest request,
            CancellationToken cancellationToken = default)
        {
            MetadataRequestCount++;
            LastMetadataRequestKey = request.Key;

            if (content is null)
            {
                return Task.FromException<GetObjectMetadataResponse>(new AmazonS3Exception("Not found")
                {
                    StatusCode = HttpStatusCode.NotFound
                });
            }

            var response = new GetObjectMetadataResponse
            {
                ContentLength = content.Length,
                ETag = "\"test-etag\"",
                LastModified = new DateTime(2026, 9, 7, 0, 0, 0, DateTimeKind.Utc)
            };
            response.Headers.ContentType = contentType;
            return Task.FromResult(response);
        }

        public override Task<GetObjectResponse> GetObjectAsync(
            GetObjectRequest request,
            CancellationToken cancellationToken = default)
        {
            ObjectRequestCount++;
            LastObjectRequestKey = request.Key;

            return Task.FromResult(new GetObjectResponse
            {
                ResponseStream = new MemoryStream(content ?? [], writable: false)
            });
        }
    }

    private sealed class LegacyProtectedMiddleware(
        IOptionsMonitor<AWSS3FileSystemOptions> options,
        IAWSS3FileSystemProvider fileSystemProvider,
        IHostingEnvironment hostingEnvironment,
        IAmazonS3 s3Client,
        IRequestParser requestParser)
        : AWSS3FileSystemMiddleware(
            AWSS3FileSystemOptions.MediaFileSystemName,
            options,
            fileSystemProvider,
            hostingEnvironment,
            s3Client,
            requestParser);

    private sealed class CustomImageWebProcessor : IImageWebProcessor
    {
        public IEnumerable<string> Commands { get; } = ["custom"];

        public FormattedImage Process(
            FormattedImage image,
            ILogger logger,
            CommandCollection commands,
            CommandParser parser,
            CultureInfo culture) => image;

        public bool RequiresTrueColorPixelFormat(
            CommandCollection commands,
            CommandParser parser,
            CultureInfo culture) => false;
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
