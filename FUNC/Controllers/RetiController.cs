using System.Formats.Tar;
using System.IO.Compression;
using System.Runtime.InteropServices;
using FUNC.Models;
using Microsoft.AspNetCore.Mvc;
using Octokit;
using static System.OperatingSystem;

namespace FUNC.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class RetiController(ILogger<RetiController> logger) : ControllerBase
    {
        private readonly ILogger<RetiController> _logger = logger;

        // Dedicated least-privilege account the Reti service runs as (non-root/non-SYSTEM).
        private const string LinuxRetiUser = "func-reti";
        private const string MacRetiUser = "_func-reti";
        private const string WinService = "Reti Validator";
        private const string WinAccount = "NT SERVICE\\Reti Validator";

        private static readonly string RetiDir = Path.Combine(Utils.appDataDir, "reti");
        private static readonly string EnvPath = Path.Combine(RetiDir, ".env");
        private static readonly string ExePath = Path.Combine(RetiDir, IsWindows() ? "reti.exe" : "reti");
        private const string LinuxUnit = "/lib/systemd/system/reti.service";
        private const string MacPlist = "/Library/LaunchDaemons/func.reti.plist";
        // The daemon's output: written by RetiService on Windows and by launchd on macOS
        // (see func.reti.plist). On Linux it goes to the journal instead.
        internal static readonly string LogPath = Path.Combine(RetiDir, "reti.log");
        private const long MaxLogBytes = 5 * 1024 * 1024;
        private const int LogTailLines = 100;

        // FUNC runs elevated and creates the reti dir (binary + .env) as root. The service
        // runs as an unprivileged account, so hand ownership of the dir to that account.
        private static async Task ApplyDirOwnership()
        {
            if (!Directory.Exists(RetiDir)) return;
            // .env holds validator config and a secret mnemonic, so restrict it to its owner.
            bool hasEnv = System.IO.File.Exists(EnvPath);
            if (IsLinux())
            {
                await Utils.EnsureUnixUser(LinuxRetiUser);
                await Utils.ChownRecursive($"{LinuxRetiUser}:{LinuxRetiUser}", RetiDir);
                if (hasEnv) System.IO.File.SetUnixFileMode(EnvPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }
            else if (IsMacOS())
            {
                // The _func-reti account is created by the pkg postinstall; skip if absent.
                if (await Utils.EnsureUnixUser(MacRetiUser))
                {
                    await Utils.ChownRecursive(MacRetiUser, RetiDir);
                    if (hasEnv) System.IO.File.SetUnixFileMode(EnvPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
                }
            }
            else if (IsWindows())
            {
                // Grant the per-service virtual account modify rights on the reti dir.
                await Utils.Exec("icacls.exe", RetiDir, "/grant", $"{WinAccount}:(OI)(CI)M", "/T");
                // Strip inherited ACEs from .env and limit access to SYSTEM, Administrators,
                // and the service account (read-only) so other local users cannot read it.
                if (hasEnv) await Utils.Exec("icacls.exe", EnvPath, "/inheritance:r", "/grant", $"{WinAccount}:R", "/grant", "SYSTEM:F", "/grant", "Administrators:F");
            }
        }

        // Bring an existing Reti service up to date after a FUNC upgrade. Idempotent.
        // Windows: (re)apply the crash-restart recovery actions, which services created
        // before the wrapper exited on a daemon crash lack, and move a legacy 3.x
        // LocalSystem service onto the virtual account (see Node.MigrateWindowsServices).
        // macOS: install the current plist, which sends the daemon's output to reti.log.
        public static async Task MigrateService()
        {
            if (IsWindows())
            {
                string qc = await Utils.Sc("qc", WinService);
                if (!qc.Contains("SERVICE_NAME")) return;
                await SetWindowsRecovery();
                if (!qc.Contains("LocalSystem")) return;
                await Utils.Sc("config", WinService, "obj=", WinAccount);
                await ApplyDirOwnership();
                await Utils.RestartWindowsService(WinService);
            }
            else if (IsMacOS())
            {
                if (!System.IO.File.Exists(MacPlist)) return;
                string template = Path.Combine(AppContext.BaseDirectory, "Templates", "func.reti.plist");
                if (System.IO.File.ReadAllText(MacPlist) == System.IO.File.ReadAllText(template)) return;
                // Leave a stopped daemon stopped: the new plist applies on its next start.
                bool running = await Utils.ServiceStatus("reti", WinService) == "Running";
                if (running) await Utils.Launchctl("bootout", "system/func.reti");
                System.IO.File.Copy(template, MacPlist, true);
                if (running) await Utils.Launchctl("bootstrap", "system", MacPlist);
            }
        }

        // The wrapper exits non-zero when the daemon dies; have the SCM restart it.
        private static Task SetWindowsRecovery() =>
            Utils.Sc("failure", WinService, "reset=", "86400", "actions=", "restart/30000/restart/30000/restart/30000");

        // The daemon's latest output, oldest first, for Diagnostics; null when there is none.
        internal static async Task<List<string>?> LogTail()
        {
            if (IsLinux())
            {
                string journal = await Utils.Exec("journalctl", "-u", "reti", "-n", LogTailLines.ToString(), "--no-pager", "-o", "short-iso");
                List<string> lines = journal.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Where(l => !l.StartsWith("-- ")).ToList();
                return lines.Count == 0 ? null : lines;
            }
            if (!System.IO.File.Exists(LogPath)) return null;
            TrimLog();
            using FileStream fs = new(LogPath, System.IO.FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            // Plenty for the last lines; skip the partial first line when starting mid-file.
            const int tailBytes = 64 * 1024;
            bool partial = fs.Length > tailBytes;
            if (partial) fs.Seek(-tailBytes, SeekOrigin.End);
            using StreamReader reader = new(fs);
            List<string> all = (await reader.ReadToEndAsync()).Split('\n').Select(l => l.TrimEnd('\r')).ToList();
            if (partial && all.Count > 0) all.RemoveAt(0);
            all = all.Where(l => l.Length > 0).ToList();
            return all.Count == 0 ? null : all.TakeLast(LogTailLines).ToList();
        }

        // launchd appends to reti.log for good, so keep one previous copy once it reaches the cap.
        // Copy-then-truncate, since launchd holds the file open (in append mode, so writes
        // carry on at the new end). On Windows RetiService rotates the log itself.
        internal static void TrimLog()
        {
            if (!IsMacOS()) return;
            try
            {
                FileInfo log = new(LogPath);
                if (!log.Exists || log.Length < MaxLogBytes) return;
                System.IO.File.Copy(LogPath, LogPath + ".1", true);
                using FileStream fs = new(LogPath, System.IO.FileMode.Open, FileAccess.Write, FileShare.ReadWrite);
                fs.SetLength(0);
            }
            catch { }
        }

        // Render the daemon's .env from validated fields. The algod URL and token come
        // from the Algorand node FUNC manages, not from the caller.
        private static string RenderEnv(RetiCreate model)
        {
            int port = Node.AlgodPort("algorand");
            string token = Node.AlgodToken("algorand");
            if (port == 0 || token.Length == 0) throw new Exception("Algorand node is not configured");
            string mnemonic = Utils.ValidateMnemonic(model.Mnemonic);
            return string.Join('\n',
            [
                $"ALGO_ALGOD_URL=http://localhost:{port}",
                $"ALGO_ALGOD_TOKEN={token}",
                $"RETI_VALIDATORID={model.ValidatorId}",
                $"RETI_NODENUM={model.NodeNum}",
                $"MANAGER_MNEMONIC={mnemonic}",
                "",
            ]);
        }

        // POST: reti
        [HttpPost]
        public async Task<ActionResult> CreateRetiService(RetiCreate model)
        {
            try
            {
                string env = RenderEnv(model);

                if (!System.IO.File.Exists(ExePath))
                {
                    await DownloadExtractReti();
                }

                if (!System.IO.File.Exists(ExePath))
                {
                    throw new Exception("Failed to download Reti");
                }

                if (System.IO.File.Exists(EnvPath))
                {
                    System.IO.File.Delete(EnvPath);
                }
                System.IO.File.WriteAllText(EnvPath, env);

                if (IsWindows())
                {
                    string binPath = Path.Combine(AppContext.BaseDirectory, "Services", "RetiService.exe");
                    // Run under the auto-managed per-service virtual account instead of LocalSystem.
                    await Utils.Sc("create", WinService, "binPath=", binPath, "obj=", WinAccount, "start=", "delayed-auto");
                    await SetWindowsRecovery();
                }
                else if (IsLinux())
                {
                    string servicePath = Path.Combine(AppContext.BaseDirectory, "Templates", "reti.service");
                    System.IO.File.Copy(servicePath, LinuxUnit, true);
                    await Utils.Systemctl("daemon-reload");
                    await Utils.Systemctl("enable", "reti");
                }
                else if (IsMacOS())
                {
                    string plistPath = Path.Combine(AppContext.BaseDirectory, "Templates", "func.reti.plist");
                    System.IO.File.Copy(plistPath, MacPlist, true);
                    await Utils.Launchctl("bootstrap", "system", MacPlist);
                }

                await ApplyDirOwnership();

                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // POST: reti/update
        [HttpPost("update")]
        public async Task<ActionResult> UpdateReti()
        {
            try
            {
                await StopRetiService();
                await DownloadExtractReti();
                await ApplyDirOwnership();
                await StartRetiService();
                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // PUT: reti/start
        [HttpPut("start")]
        public async Task<ActionResult> StartRetiService()
        {
            try
            {
                if (IsWindows())
                {
                    await Utils.Sc("start", WinService);
                }
                else if (IsLinux())
                {
                    await Utils.Systemctl("start", "reti");
                    await Utils.Systemctl("daemon-reload");
                }
                else if (IsMacOS())
                {
                    await Utils.Launchctl("bootstrap", "system", MacPlist);
                }

                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // PUT: reti/stop
        [HttpPut("stop")]
        public async Task<ActionResult> StopRetiService()
        {
            try
            {
                if (IsWindows())
                {
                    await Utils.Sc("stop", WinService);
                }
                else if (IsLinux())
                {
                    await Utils.Systemctl("stop", "reti");
                    await Utils.Systemctl("daemon-reload");
                }
                else if (IsMacOS())
                {
                    await Utils.Launchctl("bootout", "system/func.reti");
                }

                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // DELETE: reti
        [HttpDelete]
        public async Task<ActionResult> DeleteRetiService()
        {
            try
            {
                if (IsWindows())
                {
                    await Utils.Sc("delete", WinService);
                }
                else if (IsLinux())
                {
                    System.IO.File.Delete(LinuxUnit);
                    await Utils.Systemctl("daemon-reload");
                }
                else if (IsMacOS())
                {
                    await Utils.Launchctl("bootout", "system/func.reti");
                    System.IO.File.Delete(MacPlist);
                }

                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        private static async Task DownloadExtractReti()
        {
            string workspaceName = "algorandfoundation";
            string repositoryName = "reti";
            var client = new GitHubClient(new ProductHeaderValue(repositoryName));
            var latest = await client.Repository.Release.GetLatest(workspaceName, repositoryName);

            Directory.CreateDirectory(RetiDir);

            var pattern = (IsWindows() ? (RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "windows-arm64.zip" : "windows-amd64.zip")
            : IsLinux() ? (RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "linux-arm64.tar.gz" : "linux-amd64.tar.gz")
            : IsMacOS() ? (RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "darwin-arm64.tar.gz" : "darwin-amd64.tar.gz")
            : null) ?? throw new Exception("Binary Not Found");
            var asset = latest.Assets.FirstOrDefault(a => a.Name.EndsWith(pattern)) ?? throw new Exception("Binary Not Found");

            string filePath = Path.Combine(Utils.appDataDir, asset.Name);
            using var httpClient = new HttpClient();
            using var s = await httpClient.GetStreamAsync(asset.BrowserDownloadUrl);
            using FileStream fs = new(filePath, System.IO.FileMode.OpenOrCreate);
            await s.CopyToAsync(fs);
            fs.Dispose();

            if (IsWindows())
            {
                DirectoryInfo di = new(RetiDir);
                foreach (FileInfo file in di.GetFiles().Where(f => f.Name != ".env")) file.Delete();
                ZipFile.ExtractToDirectory(filePath, RetiDir);
            }
            else
            {
                using FileStream rfs = new(filePath, System.IO.FileMode.Open, FileAccess.Read);
                using GZipStream gz = new(rfs, CompressionMode.Decompress, leaveOpen: true);
                await TarFile.ExtractToDirectoryAsync(gz, RetiDir, true);
                rfs.Dispose();
            }

            System.IO.File.Delete(filePath);
        }
    }
}
