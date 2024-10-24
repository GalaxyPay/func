using RetiService;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddWindowsService();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
