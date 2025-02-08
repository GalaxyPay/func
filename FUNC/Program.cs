using FUNC;
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
    options.ListenAnyIP(3537, listenOptions =>
    {
        listenOptions.UseHttps(X509.Generate(subject: "FUNC"));
    });
});
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddTransforms(transformBuilderContext =>
    {
        transformBuilderContext.AddRequestTransform(transformContext =>
        {
            // Get the original host
            var ogPort = transformContext.HttpContext.Request.Host.Port;
            var destPort = 8080;

            // Check if it's a request to the legacy site
            if (ogPort == 1234) {
                destPort = Shared.VoiPort;
                
            } else if (ogPort == 5678) {
                // Algo Port
                destPort = Shared.AlgoPort;
            }

            var destinationPrefix = transformContext.DestinationPrefix;
            if (destinationPrefix != null)
            {
                var originalUri = new Uri(destinationPrefix);
                var newUri = new UriBuilder(originalUri)
                {
                    Port = destPort
                }.Uri;
                transformContext.DestinationPrefix = newUri.ToString();
            }
            
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

app.Run();
