using FUNC;
using FUNC.Controllers;
using Microsoft.Net.Http.Headers;
using Yarp.ReverseProxy.Transforms;

var builder = WebApplication.CreateBuilder(args);

// Developer overrides live in <appDataDir>/func.json so they survive updates:
//   { "Cors": { "Origins": [ "http://localhost:3000" ] } }
builder.Configuration.AddJsonFile(Path.Combine(Utils.appDataDir, "func.json"), optional: true, reloadOnChange: false);

// Add services to the container.

Auth.Load();
builder.Services.AddControllers(options => options.Filters.Add<SessionFilter>());
builder.Services.AddWindowsService();
builder.Services.AddSystemd();

string[] corsOrigins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? [];

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

// The UI is served from this same origin, so cross-origin access is only needed for
// development (e.g. the Vite dev server) and must be listed explicitly.
if (corsOrigins.Length > 0)
{
    builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
        policy.WithOrigins(corsOrigins).AllowAnyMethod().AllowAnyHeader()));
}

var app = builder.Build();

// Configure the HTTP request pipeline.
app.Use(async (httpContext, next) =>
{
    httpContext.Response.Headers[HeaderNames.CacheControl] = "no-cache, no-store, must-revalidate";
    await next();
});
if (corsOrigins.Length > 0) app.UseCors();
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
