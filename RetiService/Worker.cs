using System.Diagnostics;
using static System.Environment;

namespace RetiService
{
    public class Worker(ILogger<Worker> logger) : BackgroundService
    {
        private readonly ILogger<Worker> _logger = logger;
        private readonly string _exePath = Path.Combine(GetFolderPath(SpecialFolder.CommonApplicationData), @"func\reti\reti.exe");
        private readonly string _envPath = Path.Combine(GetFolderPath(SpecialFolder.CommonApplicationData), @"func\reti\.env");

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Process process = new()
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _exePath,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    Arguments = "-n mainnet -e " + _envPath + " d"
                }
            };

            try
            {
                process.Start();
                while (!stoppingToken.IsCancellationRequested)
                {
                    // Process[] pname = Process.GetProcessesByName("reti");
                    // if (pname.Length == 0) Exit(1);

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