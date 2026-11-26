using System.Diagnostics;
using static System.Environment;

namespace RetiService
{
    public class Worker(ILogger<Worker> logger) : BackgroundService
    {
        private readonly ILogger<Worker> _logger = logger;
        private static readonly string _retiDir = Path.Combine(GetFolderPath(SpecialFolder.CommonApplicationData), "func", "reti");
        private static readonly string _exePath = Path.Combine(_retiDir, "reti.exe");
        private static readonly string _envPath = Path.Combine(_retiDir, ".env");
        // FUNC's Diagnostics reads this; keep the path in step with RetiController.LogPath.
        private static readonly string _logPath = Path.Combine(_retiDir, "reti.log");
        private const long MaxLogBytes = 5 * 1024 * 1024;

        private readonly Lock _logLock = new();
        private StreamWriter? _log;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using Process process = new();
            process.StartInfo.FileName = _exePath;
            process.StartInfo.WorkingDirectory = _retiDir;
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            foreach (string arg in new[] { "-n", "mainnet", "-e", _envPath, "d" })
            {
                process.StartInfo.ArgumentList.Add(arg);
            }
            // Without a reader the daemon's output (the only record of why it fails to
            // register keys or go online) is lost, and a full pipe would block it.
            process.OutputDataReceived += (_, e) => WriteLog(e.Data);
            process.ErrorDataReceived += (_, e) => WriteLog(e.Data);

            try
            {
                OpenLog();
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                await process.WaitForExitAsync(stoppingToken);

                // The daemon exited on its own. Terminate with a non-zero exit code so the
                // Service Control Manager records a failure and the recovery actions
                // configured with `sc failure` restart the service.
                WriteLog($"reti exited with code {process.ExitCode}");
                _logger.LogError("Reti daemon exited with code {Code}", process.ExitCode);
                CloseLog();
                Exit(1);
            }
            catch (OperationCanceledException)
            {
                // Service stop requested (e.g. from services.msc): expected, so exit cleanly.
                try { process.Kill(entireProcessTree: true); } catch { }
                CloseLog();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{Message}", ex.Message);
                WriteLog($"service error: {ex.Message}");
                CloseLog();
                Exit(1);
            }
        }

        private void OpenLog()
        {
            lock (_logLock)
            {
                _log = new StreamWriter(new FileStream(_logPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite)) { AutoFlush = true };
            }
        }

        private void CloseLog()
        {
            lock (_logLock)
            {
                _log?.Dispose();
                _log = null;
            }
        }

        // Append a line, keeping one previous file (reti.log.1) once the log reaches the cap.
        private void WriteLog(string? line)
        {
            if (line == null) return;
            lock (_logLock)
            {
                if (_log == null) return;
                // Logging must never take the daemon down, so failures are swallowed.
                try
                {
                    if (_log.BaseStream.Length >= MaxLogBytes)
                    {
                        _log.Dispose();
                        _log = null;
                        try { File.Move(_logPath, _logPath + ".1", overwrite: true); } catch { }
                        _log = new StreamWriter(new FileStream(_logPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite)) { AutoFlush = true };
                    }
                    _log.WriteLine(line);
                }
                catch { }
            }
        }
    }
}
