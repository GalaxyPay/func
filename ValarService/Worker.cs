using System.Diagnostics;
using static System.Environment;

namespace ValarService
{
    public class Worker(ILogger<Worker> logger) : BackgroundService
    {
        private readonly ILogger<Worker> _logger = logger;
        private static readonly string _valarDir = Path.Combine(GetFolderPath(SpecialFolder.CommonApplicationData), "func", "valar");

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using Process process = new();
            process.StartInfo.FileName = Path.Combine(_valarDir, "venv", "Scripts", "python.exe");
            process.StartInfo.WorkingDirectory = _valarDir;
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.UseShellExecute = false;
            foreach (string arg in new[]
            {
                "-m", "valar_daemon.run_daemon",
                "--config_path", Path.Combine(_valarDir, "daemon.config"),
                "--log_path", Path.Combine(_valarDir, "log"),
            })
            {
                process.StartInfo.ArgumentList.Add(arg);
            }
            // The service virtual account has no profile; keep Python from probing user site dirs.
            process.StartInfo.Environment["PYTHONNOUSERSITE"] = "1";

            try
            {
                process.Start();
                await process.WaitForExitAsync(stoppingToken);

                // The daemon exited on its own. Terminate with a non-zero exit code so the
                // Service Control Manager records a failure and the recovery actions
                // configured with `sc failure` restart the service.
                _logger.LogError("Valar daemon exited with code {Code}", process.ExitCode);
                Exit(1);
            }
            catch (OperationCanceledException)
            {
                // Service stop requested (e.g. from services.msc): expected, so exit cleanly.
                try { process.Kill(entireProcessTree: true); } catch { }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{Message}", ex.Message);
                Exit(1);
            }
        }
    }
}
