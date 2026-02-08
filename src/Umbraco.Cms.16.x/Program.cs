using System.Text;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

builder.CreateUmbracoBuilder()
    .AddBackOffice()
    .AddWebsite()
    .AddComposers()
    .Build();

WebApplication app = builder.Build();

await app.BootUmbracoAsync();

app.UseUmbraco()
  .WithMiddleware(u =>
  {
    u.UseBackOffice();
    u.UseWebsite();
  })
  .WithEndpoints(u =>
  {
    u.UseBackOfficeEndpoints();
    u.UseWebsiteEndpoints();
  });


await app.RunAsync();
