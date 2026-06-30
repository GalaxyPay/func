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

        // FUNC runs elevated and creates the reti dir (binary + .env) as root. The service
        // runs as an unprivileged account, so hand ownership of the dir to that account.
        private static async Task ApplyDirOwnership()
        {
            string retiDir = Path.Combine(Utils.appDataDir, "reti");
            if (!Directory.Exists(retiDir)) return;
            // .env holds validator config and a secret mnemonic, so restrict it to its owner.
            string envPath = Path.Combine(retiDir, ".env");
            bool hasEnv = System.IO.File.Exists(envPath);
            if (IsLinux())
            {
                await Utils.ExecCmd($"id -u {LinuxRetiUser} >/dev/null 2>&1 || useradd --system --create-home --home-dir /var/lib/{LinuxRetiUser} --shell /usr/sbin/nologin {LinuxRetiUser}");
                await Utils.ExecCmd($"chown -R {LinuxRetiUser}:{LinuxRetiUser} '{retiDir}'");
                if (hasEnv) await Utils.ExecCmd($"chmod 600 '{envPath}'");
            }
            else if (IsMacOS())
            {
                // The _func-reti account is created by the pkg postinstall; skip if absent.
                await Utils.ExecCmd($"id -u {MacRetiUser} >/dev/null 2>&1 && chown -R {MacRetiUser} '{retiDir}'");
                if (hasEnv) await Utils.ExecCmd($"id -u {MacRetiUser} >/dev/null 2>&1 && chmod 600 '{envPath}'");
            }
            else if (IsWindows())
            {
                // Grant the per-service virtual account modify rights on the reti dir.
                await Utils.ExecCmd($"icacls \"{retiDir}\" /grant \"NT SERVICE\\Reti Validator:(OI)(CI)M\" /T");
                // Strip inherited ACEs from .env and limit access to SYSTEM, Administrators,
                // and the service account (read-only) so other local users cannot read it.
                if (hasEnv) await Utils.ExecCmd($"icacls \"{envPath}\" /inheritance:r /grant \"NT SERVICE\\Reti Validator:R\" /grant \"SYSTEM:F\" /grant \"Administrators:F\"");
            }
        }

        // Migrate a legacy 3.x LocalSystem Reti service onto the virtual account and
        // grant it the existing reti dir, so it keeps working after upgrade. See
        // Node.MigrateWindowsServices for the rationale. Self-limiting.
        public static async Task MigrateWindowsService()
        {
            if (!IsWindows()) return;
            const string svc = "Reti Validator";
            if (!(await Utils.ExecCmd($"sc qc \"{svc}\"")).Contains("LocalSystem")) return;
            await Utils.ExecCmd($"sc config \"{svc}\" obj= \"NT SERVICE\\{svc}\"");
            await ApplyDirOwnership();
            await Utils.RestartWindowsService(svc);
        }

        // POST: reti
        [HttpPost]
        public async Task<ActionResult> CreateRetiService(RetiCreate model)
        {
            try
            {
                string exePath = Path.Combine(Utils.appDataDir, "reti", "reti");
                if (IsWindows()) exePath += ".exe";

                if (!System.IO.File.Exists(exePath))
                {
                    await DownloadExtractReti();
                }

                if (!System.IO.File.Exists(exePath))
                {
                    throw new Exception("Failed to download Reti");
                }

                string envPath = Path.Combine(Utils.appDataDir, "reti", ".env");
                if (System.IO.File.Exists(envPath))
                {
                    System.IO.File.Delete(envPath);
                }
                using (StreamWriter sw = System.IO.File.CreateText(envPath))
                {
                    sw.WriteLine(model.Env);
                }

                if (IsWindows())
                {
                    string binPath = Path.Combine(AppContext.BaseDirectory, "Services", "RetiService.exe");
                    // Run under the auto-managed per-service virtual account instead of LocalSystem.
                    await Utils.ExecCmd($"sc create \"Reti Validator\" binPath= \"{binPath}\" obj= \"NT SERVICE\\Reti Validator\" start= delayed-auto");
                }
                else if (IsLinux())
                {
                    string servicePath = Path.Combine(AppContext.BaseDirectory, "Templates", "reti.service");
                    await Utils.ExecCmd($"cp {servicePath} /lib/systemd/system");
                    await Utils.ExecCmd("systemctl daemon-reload");
                    await Utils.ExecCmd("systemctl enable reti");
                }
                else if (IsMacOS())
                {
                    string plistPath = Path.Combine(AppContext.BaseDirectory, "Templates", "func.reti.plist");
                    await Utils.ExecCmd($"cp {plistPath} /Library/LaunchDaemons");
                    await Utils.ExecCmd("launchctl bootstrap system /Library/LaunchDaemons/func.reti.plist");
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
                    await Utils.ExecCmd("sc start \"Reti Validator\"");
                }
                else if (IsLinux())
                {
                    await Utils.ExecCmd($"systemctl start reti");
                    await Utils.ExecCmd($"systemctl daemon-reload");
                }
                else if (IsMacOS())
                {
                    await Utils.ExecCmd($"launchctl bootstrap system /Library/LaunchDaemons/func.reti.plist");
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
                    await Utils.ExecCmd("sc stop \"Reti Validator\"");
                }
                else if (IsLinux())
                {
                    await Utils.ExecCmd($"systemctl stop reti");
                    await Utils.ExecCmd($"systemctl daemon-reload");
                }
                else if (IsMacOS())
                {
                    await Utils.ExecCmd($"launchctl bootout system/func.reti");
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
                    await Utils.ExecCmd("sc delete \"Reti Validator\"");
                }
                else if (IsLinux())
                {
                    await Utils.ExecCmd($"rm /lib/systemd/system/reti.service");
                    await Utils.ExecCmd($"systemctl daemon-reload");
                }
                else if (IsMacOS())
                {
                    await Utils.ExecCmd($"launchctl bootout system/func.reti");
                    await Utils.ExecCmd($"rm /Library/LaunchDaemons/func.reti.plist");
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

            Directory.CreateDirectory(Path.Combine(Utils.appDataDir, "reti"));

            var pattern = (IsWindows() ? (RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "windows-arm64.zip" : "windows-amd64.zip")
            : IsLinux() ? (RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "linux-arm64.tar.gz" : "linux-amd64.tar.gz")
            : IsMacOS() ? (RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "darwin-arm64.tar.gz" : "darwin-amd64.tar.gz")
            : null) ?? throw new Exception("Binary Not Found");
            var asset = latest.Assets.FirstOrDefault(a => a.Name.EndsWith(pattern)) ?? throw new Exception("Binary Not Found");

            string filePath = Path.Combine(Utils.appDataDir, asset.Name);
            string destDir = Path.Combine(Utils.appDataDir, "reti");
            using var httpClient = new HttpClient();
            using var s = await httpClient.GetStreamAsync(asset.BrowserDownloadUrl);
            using FileStream fs = new(filePath, System.IO.FileMode.OpenOrCreate);
            await s.CopyToAsync(fs);
            fs.Dispose();

            if (IsWindows())
            {
                DirectoryInfo di = new(destDir);
                foreach (FileInfo file in di.GetFiles().Where(f => f.Name != ".env")) file.Delete();
                ZipFile.ExtractToDirectory(filePath, destDir);
            }
            else
            {
                using FileStream rfs = new(filePath, System.IO.FileMode.Open, FileAccess.Read);
                using GZipStream gz = new(rfs, CompressionMode.Decompress, leaveOpen: true);
                await TarFile.ExtractToDirectoryAsync(gz, destDir, true);
                rfs.Dispose();
            }

            System.IO.File.Delete(filePath);
        }
    }
}
