using AF.Umbraco.S3.Media.Storage.Interfaces;
using AF.Umbraco.S3.Media.Storage.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace AF.Umbraco.S3.Media.Storage.Middlewares
{
    public class AWSS3SmokeTestsMiddleware(IAWSS3FileSystemProvider fileSystemProvider, ILogger<AWSS3SmokeTestsMiddleware> logger) : IMiddleware
    {
        public async Task InvokeAsync(HttpContext context, RequestDelegate next)
        {
            if (HttpMethods.IsGet(context.Request.Method) && context.Request.Path == "/smoke/health")
            {
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync("{\"status\":\"ok\"}");
                return;
            }

            if (HttpMethods.IsPost(context.Request.Method) && context.Request.Path == "/smoke/media-upload")
            {
                try
                {
                    IAWSS3FileSystem fileSystem = fileSystemProvider.GetFileSystem(AWSS3FileSystemOptions.MediaFileSystemName);
                    string path = $"/smoke/{Guid.NewGuid():N}.txt";

                    using var payload = new MemoryStream(Encoding.UTF8.GetBytes("smoke-upload"));
                    fileSystem.AddFile(path, payload, true);

                    if (!fileSystem.FileExists(path))
                    {
                        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                        await context.Response.WriteAsync("Uploaded file was not found in media storage.");
                        return;
                    }

                    using Stream stream = fileSystem.OpenFile(path);
                    using var reader = new StreamReader(stream, Encoding.UTF8);
                    string content = await reader.ReadToEndAsync();

                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync($"{{\"status\":\"ok\",\"content\":\"{content}\"}}");
                    return;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Smoke test media upload failed.");
                    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                    await context.Response.WriteAsync("Smoke test media upload failed.");
                    return;
                }
            }

            await next(context);
        }
    }
}
