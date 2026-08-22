using AF.Umbraco.S3.Media.Storage.Interfaces;
using AF.Umbraco.S3.Media.Storage.Middlewares;
using AF.Umbraco.S3.Media.Storage.Options;
using AF.Umbraco.S3.Media.Storage.Providers;
using Amazon.S3;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp.Web.Commands;
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
        context.Request.QueryString = new QueryString("?width=320");
        bool nextInvoked = false;

        AWSS3FileSystemMiddleware middleware = CreateMiddleware(hasImageSharpCommands: true);

        await middleware.InvokeAsync(context, _ =>
        {
            nextInvoked = true;
            return Task.CompletedTask;
        });

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

        AWSS3FileSystemMiddleware middleware = CreateMiddleware(hasImageSharpCommands: false);

        await middleware.InvokeAsync(context, _ =>
        {
            nextInvoked = true;
            return Task.CompletedTask;
        });

        Assert.True(nextInvoked);
    }

    private static AWSS3FileSystemMiddleware CreateMiddleware(bool hasImageSharpCommands) =>
        new(
            new StaticOptionsMonitor(new AWSS3FileSystemOptions
            {
                BucketName = "test-bucket",
                VirtualPath = "~/media"
            }),
            new TestFileSystemProvider(),
            new TestHostingEnvironment(),
            null!,
            new TestRequestParser(hasImageSharpCommands));

    private sealed class StaticOptionsMonitor(AWSS3FileSystemOptions options) : IOptionsMonitor<AWSS3FileSystemOptions>
    {
        public AWSS3FileSystemOptions CurrentValue => options;

        public AWSS3FileSystemOptions Get(string? name) => options;

        public IDisposable? OnChange(Action<AWSS3FileSystemOptions, string?> listener) => null;
    }

    private sealed class TestRequestParser(bool hasImageSharpCommands) : IRequestParser
    {
        public CommandCollection ParseRequestCommands(HttpContext context)
        {
            var commands = new CommandCollection();

            if (hasImageSharpCommands)
            {
                commands.Add("width", "320");
            }

            return commands;
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
