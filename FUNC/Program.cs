using FUNC;
using FUNC.Controllers;
using Microsoft.Net.Http.Headers;
using Yarp.ReverseProxy.Transforms;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddWindowsService();
builder.Services.AddSystemd();
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(3536);
    var cert = X509.Generate(subject: "FUNC");
    options.ListenAnyIP(3537, listenOptions =>
    {
        listenOptions.UseHttps(cert);
    });
    options.ListenAnyIP(3538, listenOptions =>
    {
        listenOptions.UseHttps(cert);
    });
});
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddTransforms(transformBuilderContext =>
    {
        transformBuilderContext.AddRequestTransform(transformContext =>
        {
            var ogPort = transformContext.HttpContext.Request.Host.Port;
            var destPort = ogPort == 3538 ? Shared.VoiPort : Shared.AlgoPort;
            var newUri = new UriBuilder(transformContext.DestinationPrefix) { Port = destPort }.Uri;
            transformContext.DestinationPrefix = newUri.ToString();
            return ValueTask.CompletedTask;
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
app.MapReverseProxy();

// One-time upgrade migration of legacy LocalSystem Windows services onto the
// least-privilege virtual accounts. Runs after startup so it neither delays the
// Windows service-start handshake nor blocks Kestrel; it's a no-op once migrated.
app.Lifetime.ApplicationStarted.Register(() =>
{
    _ = Task.Run(async () =>
    {
        try
        {
            await Node.MigrateWindowsServices();
            await RetiController.MigrateWindowsService();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Service migration error: {ex.Message}");
        }
    });
});

app.Run();
