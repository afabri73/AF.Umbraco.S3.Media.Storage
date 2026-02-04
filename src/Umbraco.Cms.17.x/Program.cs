using AF.Umbraco.S3.Media.Storage.Extensions;

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

await app.RunAsync();
