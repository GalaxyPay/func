using Microsoft.Net.Http.Headers;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddWindowsService();
builder.Services.AddSystemd();
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.ListenLocalhost(3536);
});

var app = builder.Build();

// Configure the HTTP request pipeline.
app.Use(async (httpContext, next) =>
{
    httpContext.Response.Headers[HeaderNames.CacheControl] = "no-cache, no-store, must-revalidate";
    await next();
});
var allowedDomains = new[]
{
    "http://localhost:3000",
};
app.UseCors(options => options.WithOrigins(allowedDomains).AllowAnyMethod().AllowAnyHeader());
app.MapControllers();
app.UseFileServer();

app.Run();
