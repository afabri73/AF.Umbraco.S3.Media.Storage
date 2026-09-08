using AF.Umbraco.S3.Media.Storage.Composers;
using AF.Umbraco.S3.Media.Storage.Core;
using AF.Umbraco.S3.Media.Storage.Extensions;
using AF.Umbraco.S3.Media.Storage.Interfaces;
using AF.Umbraco.S3.Media.Storage.Options;
using AF.Umbraco.S3.Media.Storage.Providers;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp.Web;
using SixLabors.ImageSharp.Web.Caching;
using SixLabors.ImageSharp.Web.DependencyInjection;
using SixLabors.ImageSharp.Web.Middleware;
using SixLabors.ImageSharp.Web.Providers;
using SixLabors.ImageSharp.Web.Resolvers;
using System.Net;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.Hosting;
using Umbraco.Cms.Imaging.ImageSharp;
using Xunit;

namespace AF.Umbraco.S3.Media.Storage.Tests;

/// <summary>
/// Verifies ImageSharp source resolution for existing, missing, and unauthorized S3 objects.
/// </summary>
public sealed class AWSS3FileSystemImageProviderTests
{
    /// <summary>
    /// Verifies package composition runs after Umbraco configures its default ImageSharp provider.
    /// </summary>
    [Fact]
    public void Composer_RunsAfterUmbracoImageSharpComposer()
    {
        ComposeAfterAttribute attribute = Assert.Single(
            typeof(AWSS3Composer).GetCustomAttributes(typeof(ComposeAfterAttribute), inherit: false)
                .Cast<ComposeAfterAttribute>());

        Assert.Equal(typeof(ImageSharpComposer), attribute.RequiredType);
    }

    /// <summary>
    /// Verifies S3 ImageSharp services replace the cache and precede Umbraco's catch-all web-root provider.
    /// </summary>
    [Fact]
    public void ConfigureImageSharp_PreservesWebRootProviderBehindS3Provider()
    {
        var services = new ServiceCollection();
        services
            .AddImageSharp()
            .ClearProviders()
            .AddProvider<WebRootImageProvider>();

        AWSS3MediaFileSystemExtensions.ConfigureImageSharp(services);

        ServiceDescriptor[] providerDescriptors = services
            .Where(descriptor => descriptor.ServiceType == typeof(IImageProvider))
            .ToArray();

        Assert.Collection(
            providerDescriptors,
            descriptor => Assert.Equal(typeof(AWSS3FileSystemImageProvider), descriptor.ImplementationType),
            descriptor => Assert.Equal(typeof(WebRootImageProvider), descriptor.ImplementationType));
        Assert.Equal(
            typeof(AWSS3FileSystemImageCache),
            Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IImageCache)).ImplementationType);
        Assert.Equal(
            typeof(AWSS3ScopedCacheHash),
            Assert.Single(services, descriptor => descriptor.ServiceType == typeof(ICacheHash)).ImplementationType);
    }

    /// <summary>
    /// Verifies the provider returns no resolver when S3 reports a missing object.
    /// </summary>
    [Fact]
    public async Task GetAsync_MissingObject_ReturnsNull()
    {
        using var s3Client = CreateClientWithMetadataFailure(HttpStatusCode.NotFound);
        using ServiceProvider services = CreateServices(s3Client);
        AWSS3FileSystemImageProvider provider = GetProvider(services);
        DefaultHttpContext context = CreateContext("/media/missing.png");

        IImageResolver? resolver = await provider.GetAsync(context);

        Assert.Null(resolver);
        Assert.Equal(1, s3Client.MetadataRequestCount);
        Assert.Equal("test-bucket", s3Client.LastMetadataRequestBucket);
        Assert.Equal("media/missing.png", s3Client.LastMetadataRequestKey);
    }

    /// <summary>
    /// Verifies ImageSharp passes a missing source to the next middleware, which can return HTTP 404.
    /// </summary>
    [Fact]
    public async Task ImageSharpPipeline_MissingObject_InvokesNextMiddlewareWith404()
    {
        using var s3Client = CreateClientWithMetadataFailure(HttpStatusCode.NotFound);
        using ServiceProvider services = CreateServices(s3Client);
        bool nextInvoked = false;
        RequestDelegate next = context =>
        {
            nextInvoked = true;
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return Task.CompletedTask;
        };
        ImageSharpMiddleware middleware = ActivatorUtilities.CreateInstance<ImageSharpMiddleware>(services, next);
        DefaultHttpContext context = CreateContext("/media/missing-pipeline.png", "?width=100");
        context.RequestServices = services;

        await middleware.Invoke(context);

        Assert.True(nextInvoked);
        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
        Assert.Equal(1, s3Client.MetadataRequestCount);
    }

    /// <summary>
    /// Verifies the resolver reuses metadata obtained by the provider and performs no duplicate HEAD request.
    /// </summary>
    [Fact]
    public async Task GetAsync_ExistingObject_ReusesMetadataAndResolvedKey()
    {
        byte[] content = [1, 2, 3, 4];
        var lastModifiedUtc = new DateTime(2026, 9, 8, 10, 30, 0, DateTimeKind.Utc);
        using var cancellationSource = new CancellationTokenSource();
        using var s3Client = new TestAmazonS3Client
        {
            MetadataHandler = (_, _) => Task.FromResult(new GetObjectMetadataResponse
            {
                ContentLength = content.Length,
                LastModified = lastModifiedUtc
            }),
            ObjectHandler = (_, _) => Task.FromResult(new GetObjectResponse
            {
                ResponseStream = new MemoryStream(content, writable: false)
            })
        };
        using ServiceProvider services = CreateServices(s3Client);
        AWSS3FileSystemImageProvider provider = GetProvider(services);
        DefaultHttpContext context = CreateContext("/media/existing.png");
        context.RequestAborted = cancellationSource.Token;

        IImageResolver resolver = Assert.IsAssignableFrom<IImageResolver>(await provider.GetAsync(context));
        ImageMetadata metadata = await resolver.GetMetaDataAsync();

        Assert.Equal(lastModifiedUtc, metadata.LastWriteTimeUtc);
        Assert.Equal(content.Length, metadata.ContentLength);
        Assert.Equal(1, s3Client.MetadataRequestCount);
        Assert.Equal(cancellationSource.Token, s3Client.LastMetadataCancellationToken);
        Assert.Equal("media/existing.png", s3Client.LastMetadataRequestKey);

        await using Stream stream = await resolver.OpenReadAsync();
        using var copy = new MemoryStream();
        await stream.CopyToAsync(copy);

        Assert.Equal(content, copy.ToArray());
        Assert.Equal(1, s3Client.MetadataRequestCount);
        Assert.Equal(1, s3Client.ObjectRequestCount);
        Assert.Equal("media/existing.png", s3Client.LastObjectRequestKey);
    }

    /// <summary>
    /// Verifies authorization failures remain visible instead of being converted to missing media.
    /// </summary>
    /// <param name="statusCode">The authorization status returned by S3.</param>
    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task GetAsync_AuthorizationFailure_PropagatesAmazonS3Exception(HttpStatusCode statusCode)
    {
        using var s3Client = CreateClientWithMetadataFailure(statusCode);
        using ServiceProvider services = CreateServices(s3Client);
        AWSS3FileSystemImageProvider provider = GetProvider(services);
        DefaultHttpContext context = CreateContext("/media/restricted.png");

        AmazonS3Exception exception = await Assert.ThrowsAsync<AmazonS3Exception>(
            () => provider.GetAsync(context));

        Assert.Equal(statusCode, exception.StatusCode);
        Assert.Equal(1, s3Client.MetadataRequestCount);
    }

    private static TestAmazonS3Client CreateClientWithMetadataFailure(HttpStatusCode statusCode) =>
        new()
        {
            MetadataHandler = (_, _) => Task.FromException<GetObjectMetadataResponse>(new AmazonS3Exception($"S3 returned {statusCode}")
            {
                StatusCode = statusCode
            })
        };

    private static ServiceProvider CreateServices(IAmazonS3 s3Client)
    {
        AWSS3FileSystemOptions options = S3TestInfrastructure.CreateOptions();
        IAWSS3FileSystem fileSystem = S3TestInfrastructure.CreateFileSystem(s3Client);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddImageSharp();
        services.RemoveAll<IImageProvider>();
        services.AddSingleton<IOptionsMonitor<AWSS3FileSystemOptions>>(new StaticOptionsMonitor(options));
        services.AddSingleton<IAWSS3FileSystemProvider>(new StaticFileSystemProvider(fileSystem));
        services.AddSingleton<IHostingEnvironment, TestUmbracoHostingEnvironment>();
        services.AddSingleton<Microsoft.AspNetCore.Hosting.IWebHostEnvironment, TestWebHostEnvironment>();
        services.AddSingleton(s3Client);
        services.AddSingleton<IImageProvider, AWSS3FileSystemImageProvider>();
        return services.BuildServiceProvider();
    }

    private static AWSS3FileSystemImageProvider GetProvider(IServiceProvider services) =>
        Assert.IsType<AWSS3FileSystemImageProvider>(services.GetRequiredService<IImageProvider>());

    private static DefaultHttpContext CreateContext(string path, string queryString = "")
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("example.com");
        context.Request.Path = path;
        context.Request.QueryString = new QueryString(queryString);
        context.Response.Body = new MemoryStream();
        return context;
    }
}
