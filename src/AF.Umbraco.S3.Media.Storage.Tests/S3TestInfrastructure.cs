using AF.Umbraco.S3.Media.Storage.Core;
using AF.Umbraco.S3.Media.Storage.Interfaces;
using AF.Umbraco.S3.Media.Storage.Options;
using AF.Umbraco.S3.Media.Storage.Providers;
using AF.Umbraco.S3.Media.Storage.Resolvers;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Hosting;

namespace AF.Umbraco.S3.Media.Storage.Tests;

/// <summary>
/// Provides reusable S3 and hosting test doubles for filesystem and ImageSharp regression tests.
/// </summary>
internal static class S3TestInfrastructure
{
    /// <summary>
    /// Creates the standard named filesystem options used by S3 regression tests.
    /// </summary>
    /// <returns>The configured test options.</returns>
    internal static AWSS3FileSystemOptions CreateOptions() =>
        new()
        {
            BucketName = "test-bucket",
            VirtualPath = "~/media"
        };

    /// <summary>
    /// Creates a real filesystem instance backed by the supplied test S3 client.
    /// </summary>
    /// <param name="s3Client">The test S3 client.</param>
    /// <returns>The configured filesystem.</returns>
    internal static AWSS3FileSystem CreateFileSystem(IAmazonS3 s3Client) =>
        new(
            CreateOptions(),
            new TestUmbracoHostingEnvironment(),
            new FileExtensionContentTypeProvider(),
            NullLogger<AWSS3FileSystem>.Instance,
            new TestMimeTypeResolver(),
            s3Client,
            new HttpContextAccessor());
}

/// <summary>
/// Records AWS requests and allows each asynchronous response to be configured independently.
/// </summary>
internal sealed class TestAmazonS3Client : AmazonS3Client
{
    /// <summary>
    /// Initializes a client that never uses network credentials.
    /// </summary>
    internal TestAmazonS3Client()
        : base(new AnonymousAWSCredentials(), RegionEndpoint.USEast1)
    {
    }

    internal Func<GetObjectMetadataRequest, CancellationToken, Task<GetObjectMetadataResponse>>? MetadataHandler { get; init; }

    internal Func<GetObjectRequest, CancellationToken, Task<GetObjectResponse>>? ObjectHandler { get; init; }

    internal Func<ListObjectsRequest, CancellationToken, Task<ListObjectsResponse>>? ListHandler { get; init; }

    internal Func<DeleteObjectRequest, CancellationToken, Task<DeleteObjectResponse>>? DeleteHandler { get; init; }

    internal int MetadataRequestCount { get; private set; }

    internal int ObjectRequestCount { get; private set; }

    internal int ListRequestCount { get; private set; }

    internal int DeleteRequestCount { get; private set; }

    internal string? LastMetadataRequestKey { get; private set; }

    internal string? LastMetadataRequestBucket { get; private set; }

    internal string? LastObjectRequestKey { get; private set; }

    internal CancellationToken LastMetadataCancellationToken { get; private set; }

    public override Task<GetObjectMetadataResponse> GetObjectMetadataAsync(
        GetObjectMetadataRequest request,
        CancellationToken cancellationToken = default)
    {
        MetadataRequestCount++;
        LastMetadataRequestKey = request.Key;
        LastMetadataRequestBucket = request.BucketName;
        LastMetadataCancellationToken = cancellationToken;
        return MetadataHandler?.Invoke(request, cancellationToken)
            ?? Task.FromResult(new GetObjectMetadataResponse());
    }

    public override Task<GetObjectResponse> GetObjectAsync(
        GetObjectRequest request,
        CancellationToken cancellationToken = default)
    {
        ObjectRequestCount++;
        LastObjectRequestKey = request.Key;
        return ObjectHandler?.Invoke(request, cancellationToken)
            ?? Task.FromResult(new GetObjectResponse { ResponseStream = new MemoryStream() });
    }

    public override Task<ListObjectsResponse> ListObjectsAsync(
        ListObjectsRequest request,
        CancellationToken cancellationToken = default)
    {
        ListRequestCount++;
        return ListHandler?.Invoke(request, cancellationToken)
            ?? Task.FromResult(new ListObjectsResponse());
    }

    public override Task<DeleteObjectResponse> DeleteObjectAsync(
        DeleteObjectRequest request,
        CancellationToken cancellationToken = default)
    {
        DeleteRequestCount++;
        return DeleteHandler?.Invoke(request, cancellationToken)
            ?? Task.FromResult(new DeleteObjectResponse());
    }
}

/// <summary>
/// Supplies one filesystem instance for a named provider lookup.
/// </summary>
/// <param name="fileSystem">The filesystem returned for every name.</param>
internal sealed class StaticFileSystemProvider(IAWSS3FileSystem fileSystem) : IAWSS3FileSystemProvider
{
    public IAWSS3FileSystem GetFileSystem(string name) => fileSystem;
}

/// <summary>
/// Supplies immutable named options to components under test.
/// </summary>
/// <param name="options">The options returned for every name.</param>
internal sealed class StaticOptionsMonitor(AWSS3FileSystemOptions options) : IOptionsMonitor<AWSS3FileSystemOptions>
{
    public AWSS3FileSystemOptions CurrentValue => options;

    public AWSS3FileSystemOptions Get(string? name) => options;

    public IDisposable? OnChange(Action<AWSS3FileSystemOptions, string?> listener) => null;
}

/// <summary>
/// Resolves a stable binary content type for filesystem tests.
/// </summary>
internal sealed class TestMimeTypeResolver : IMimeTypeResolver
{
    public string Resolve(string path) => "application/octet-stream";
}

/// <summary>
/// Maps Umbraco virtual paths without requiring a running host.
/// </summary>
internal sealed class TestUmbracoHostingEnvironment : IHostingEnvironment
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

/// <summary>
/// Provides ImageSharp with minimal host paths and inert file providers.
/// </summary>
internal sealed class TestWebHostEnvironment : Microsoft.AspNetCore.Hosting.IWebHostEnvironment
{
    public string ApplicationName { get; set; } = "AF.Umbraco.S3.Media.Storage.Tests";

    public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();

    public string WebRootPath { get; set; } = Path.GetTempPath();

    public string EnvironmentName { get; set; } = Microsoft.Extensions.Hosting.Environments.Development;

    public string ContentRootPath { get; set; } = Path.GetTempPath();

    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}
