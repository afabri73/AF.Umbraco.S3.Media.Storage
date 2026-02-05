using System.Text;
using AF.Umbraco.S3.Media.Storage.Interfaces;
using AF.Umbraco.S3.Media.Storage.Extensions;
using AF.Umbraco.S3.Media.Storage.Options;
using AF.Umbraco.S3.Media.Storage.Providers;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

builder.CreateUmbracoBuilder()
    .AddBackOffice()
    .AddWebsite()
    .AddComposers()
    .AddAWSS3MediaFileSystem()
    .Build();

WebApplication app = builder.Build();

await app.BootUmbracoAsync();

app.UseUmbraco()
  .WithMiddleware(u =>
  {
    u.UseBackOffice();
    u.UseWebsite();
    u.UseAWSS3MediaFileSystem();
  })
  .WithEndpoints(u =>
  {
    u.UseBackOfficeEndpoints();
    u.UseWebsiteEndpoints();
  });

if (Environment.GetEnvironmentVariable("AF_SMOKE_TESTS") == "1")
{
    app.MapGet("/smoke/health", () => Results.Ok(new { status = "ok" }));

    app.MapPost("/smoke/media-upload", (HttpContext httpContext) =>
    {
        IAWSS3FileSystemProvider fileSystemProvider = httpContext.RequestServices.GetRequiredService<IAWSS3FileSystemProvider>();
        IAWSS3FileSystem fileSystem = fileSystemProvider.GetFileSystem(AWSS3FileSystemOptions.MediaFileSystemName);
        string path = $"/smoke/{Guid.NewGuid():N}.txt";

        using var payload = new MemoryStream(Encoding.UTF8.GetBytes("smoke-upload"));
        fileSystem.AddFile(path, payload, true);

        bool exists = fileSystem.FileExists(path);
        if (!exists)
        {
            return Results.Problem("Uploaded file was not found in media storage.");
        }

        using Stream stream = fileSystem.OpenFile(path);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        string content = reader.ReadToEnd();

        fileSystem.DeleteFile(path);

        return Results.Ok(new { path, exists, content });
    });
}

await app.RunAsync();
