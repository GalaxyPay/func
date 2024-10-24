using NodeService;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddWindowsService();
builder.Services.AddSingleton(new ArgsService(args));
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
