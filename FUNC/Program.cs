using Microsoft.Net.Http.Headers;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddWindowsService();
builder.Services.AddSystemd();
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.ListenAnyIP(3536);
});

var app = builder.Build();

// Configure the HTTP request pipeline.
app.Use(async (httpContext, next) =>
{
    httpContext.Response.Headers[HeaderNames.CacheControl] = "no-cache, no-store, must-revalidate";
    await next();
});
app.UseCors(options => options.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
app.MapControllers();
app.UseFileServer();

app.Run();
