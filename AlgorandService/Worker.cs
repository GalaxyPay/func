using System.Diagnostics;
using static System.Environment;

namespace AlgorandService
{
    public class Worker(ILogger<Worker> logger) : BackgroundService
    {
        private readonly ILogger<Worker> _logger = logger;
        private readonly string _exePath = Path.Combine(GetFolderPath(SpecialFolder.CommonApplicationData), @"AvmWinNode\algod.exe");
        private readonly string _dataPath = Path.Combine(GetFolderPath(SpecialFolder.CommonApplicationData), @"AvmWinNode\algorand");

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Process process = new()
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _exePath,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    Arguments = "-d " + _dataPath
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

                Environment.Exit(1);
            }
        }
    }
}
