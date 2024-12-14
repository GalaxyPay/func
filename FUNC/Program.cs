using FUNC;
using Microsoft.Net.Http.Headers;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddWindowsService();
builder.Services.AddSystemd();
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(3536);
    options.ListenAnyIP(3537, listenOptions =>
    {
        listenOptions.UseHttps(X509.Generate(subject: "FUNC"));
    });
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
