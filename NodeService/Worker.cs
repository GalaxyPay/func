using System.Diagnostics;
using static System.Environment;

namespace NodeService
{
    public class Worker(ILogger<Worker> logger, ArgsService argsService) : BackgroundService
    {
        private readonly ILogger<Worker> _logger = logger;
        private readonly ArgsService _argsService = argsService;
        private readonly string _appData = Path.Combine(GetFolderPath(SpecialFolder.CommonApplicationData), @"func\");

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Process process = new()
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _appData + @"bin\algod.exe",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    Arguments = "-d " + _appData + _argsService.GetArgs()[0]
                }
            };

            try
            {
                process.Start();
                while (!stoppingToken.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                // When the stopping token is canceled, for example, a call made from services.msc,
                // we shouldn't exit with a non-zero exit code. In other words, this is expected...

                process.Kill();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{Message}", ex.Message);

                // Terminates this process and returns an exit code to the operating system.
                // This is required to avoid the 'BackgroundServiceExceptionBehavior', which
                // performs one of two scenarios:
                // 1. When set to "Ignore": will do nothing at all, errors cause zombie services.
                // 2. When set to "StopHost": will cleanly stop the host, and log errors.
                //
                // In order for the Windows Service Management system to leverage configured
                // recovery options, we need to terminate the process with a non-zero exit code.

                Exit(1);
            }
        }
    }
}