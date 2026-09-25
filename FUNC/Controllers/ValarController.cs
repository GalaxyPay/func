using System.Formats.Tar;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using FUNC.Models;
using Microsoft.AspNetCore.Mvc;
using Octokit;
using static System.OperatingSystem;

namespace FUNC.Controllers
{
    // The Valar daemon (https://github.com/ValarStaking/valar) is a pure-Python package on
    // PyPI. FUNC provides it a self-contained runtime: it downloads the `uv` binary from
    // GitHub Releases, uses it to install a managed CPython and a venv holding the
    // valar_daemon package under <appDataDir>/valar, then runs the daemon as an OS
    // service in the same way as Reti.
    [ApiController]
    [Route("[controller]")]
    public partial class ValarController(ILogger<ValarController> logger) : ControllerBase
    {
        private readonly ILogger<ValarController> _logger = logger;

        // Dedicated least-privilege account the Valar service runs as (non-root/non-SYSTEM).
        private const string LinuxValarUser = "func-valar";
        private const string MacValarUser = "_func-valar";
        private const string WinService = "Valar Daemon";
        private const string WinAccount = "NT SERVICE\\Valar Daemon";

        private const string PackageName = "valar_daemon";
        private const string PythonVersion = "3.12";

        private static readonly string ValarDir = Path.Combine(Utils.appDataDir, "valar");
        private static readonly string UvPath = Path.Combine(ValarDir, IsWindows() ? "uv.exe" : "uv");
        private static readonly string PythonDir = Path.Combine(ValarDir, "python");
        private static readonly string VenvDir = Path.Combine(ValarDir, "venv");
        private static readonly string VenvPython = IsWindows()
            ? Path.Combine(VenvDir, "Scripts", "python.exe")
            : Path.Combine(VenvDir, "bin", "python");
        private static readonly string ConfigPath = Path.Combine(ValarDir, "daemon.config");
        private static readonly string LogDir = Path.Combine(ValarDir, "log");

        [GeneratedRegex(@"^\s*validator_ad_id_list\s*=\s*\[\d+(\s*,\s*\d+)*\]\s*$", RegexOptions.Multiline)]
        private static partial Regex AdIdListLine();

        // The daemon eval()s validator_ad_id_list, so only accept a plain list of integers.
        private static void ValidateConfig(string config)
        {
            if (!AdIdListLine().IsMatch(config))
                throw new Exception("validator_ad_id_list must be a list of numeric Validator Ad IDs");
        }

        public static async Task<DaemonStatus> GetStatus()
        {
            string query = string.Empty;
            if (IsWindows())
            {
                query = await Utils.ExecCmd($"sc query \"{WinService}\"");
            }
            else if (IsLinux())
            {
                query = await Utils.ExecCmd("systemctl show valar --property=LoadState --property=ActiveState");
            }
            else if (IsMacOS())
            {
                query = await Utils.ExecCmd("launchctl list | grep -i func.valar || (test -f /Library/LaunchDaemons/func.valar.plist && echo stopped || echo none)");
            }
            string serviceStatus = Utils.ParseServiceStatus(query);

            // The daemon has no health endpoint, but it appends to its log every loop
            // (15s by default), so a recently written log file is the liveness signal.
            // Null means the service is up but has not logged yet (e.g. just started).
            string? exeStatus = null;
            if (serviceStatus == "Running")
            {
                DateTime? newest = NewestLogWrite();
                if (newest != null)
                    exeStatus = DateTime.UtcNow - newest.Value < TimeSpan.FromMinutes(5) ? "Running" : "Stopped";
            }

            return new DaemonStatus
            {
                ServiceStatus = serviceStatus,
                Version = InstalledVersion(),
                ExeStatus = exeStatus,
            };
        }

        private static DateTime? NewestLogWrite()
        {
            try
            {
                if (!Directory.Exists(LogDir)) return null;
                var files = Directory.GetFiles(LogDir, "*", SearchOption.AllDirectories);
                if (files.Length == 0) return null;
                return files.Max(System.IO.File.GetLastWriteTimeUtc);
            }
            catch
            {
                return null;
            }
        }

        // Read the version from the venv's dist-info folder name; no pip in the venv and
        // no process spawn needed.
        private static string? InstalledVersion()
        {
            try
            {
                string? sitePackages = null;
                if (IsWindows())
                {
                    sitePackages = Path.Combine(VenvDir, "Lib", "site-packages");
                }
                else
                {
                    string lib = Path.Combine(VenvDir, "lib");
                    if (Directory.Exists(lib))
                    {
                        string? py = Directory.GetDirectories(lib, "python3.*").FirstOrDefault();
                        if (py != null) sitePackages = Path.Combine(py, "site-packages");
                    }
                }
                if (sitePackages == null || !Directory.Exists(sitePackages)) return null;
                string? distInfo = Directory.GetDirectories(sitePackages, $"{PackageName}-*.dist-info").FirstOrDefault();
                if (distInfo == null) return null;
                string name = Path.GetFileName(distInfo);
                return name[(PackageName.Length + 1)..^".dist-info".Length];
            }
            catch
            {
                return null;
            }
        }

        // FUNC runs elevated and installs the runtime as root/SYSTEM. The service runs as
        // an unprivileged account, so hand it ownership of the whole valar dir: besides
        // daemon.config (which holds a secret mnemonic) the daemon writes a swap copy of
        // the config and its logs into the dir, so the dir itself must be private too.
        private static async Task ApplyDirOwnership()
        {
            if (!Directory.Exists(ValarDir)) return;
            bool hasConfig = System.IO.File.Exists(ConfigPath);
            if (IsLinux())
            {
                await Utils.ExecCmd($"id -u {LinuxValarUser} >/dev/null 2>&1 || useradd --system --create-home --home-dir /var/lib/{LinuxValarUser} --shell /usr/sbin/nologin {LinuxValarUser}");
                await Utils.ExecCmd($"chown -R {LinuxValarUser}:{LinuxValarUser} '{ValarDir}'");
                await Utils.ExecCmd($"chmod 700 '{ValarDir}'");
                if (hasConfig) await Utils.ExecCmd($"chmod 600 '{ConfigPath}'");
            }
            else if (IsMacOS())
            {
                // The _func-valar account is created by the pkg postinstall. Without it
                // launchd cannot start the job, so fail loudly rather than skip.
                string uid = await Utils.ExecCmd($"id -u {MacValarUser} 2>/dev/null");
                if (string.IsNullOrWhiteSpace(uid))
                    throw new Exception($"The {MacValarUser} account is missing. Reinstall FUNC to create it.");
                await Utils.ExecCmd($"chown -R {MacValarUser} '{ValarDir}'");
                await Utils.ExecCmd($"chmod 700 '{ValarDir}'");
                if (hasConfig) await Utils.ExecCmd($"chmod 600 '{ConfigPath}'");
            }
            else if (IsWindows())
            {
                // Strip the ACEs inherited from ProgramData (which let all local users read)
                // and grant only SYSTEM, Administrators and the per-service virtual account.
                await Utils.ExecCmd($"icacls \"{ValarDir}\" /inheritance:r /grant \"SYSTEM:(OI)(CI)F\" /grant \"Administrators:(OI)(CI)F\" /grant \"{WinAccount}:(OI)(CI)M\" /T /Q");
                if (hasConfig) await Utils.ExecCmd($"icacls \"{ConfigPath}\" /inheritance:r /grant \"{WinAccount}:R\" /grant \"SYSTEM:F\" /grant \"Administrators:F\"");
            }
        }

        // POST: valar
        [HttpPost]
        public async Task<ActionResult> CreateValarService(ValarCreate model)
        {
            try
            {
                var status = await GetStatus();
                if (status.ServiceStatus == "Running" || status.ServiceStatus == "Stopped")
                    throw new Exception("Valar service already exists");
                // Upstream's own guide names its unit valar.service too; an /etc unit would
                // shadow ours and FUNC would end up driving the user's copy.
                if (IsLinux() && System.IO.File.Exists("/etc/systemd/system/valar.service"))
                    throw new Exception("A valar.service unit already exists on this system");
                ValidateConfig(model.Config);

                await EnsureUv();
                await InstallDaemon(upgrade: false);

                if (System.IO.File.Exists(ConfigPath))
                {
                    System.IO.File.Delete(ConfigPath);
                }
                using (StreamWriter sw = System.IO.File.CreateText(ConfigPath))
                {
                    sw.WriteLine(model.Config);
                }
                // Log activity is the liveness signal, so start from an empty log dir:
                // logs left over from a removed install would otherwise report the new
                // daemon as running before it has started.
                if (Directory.Exists(LogDir)) Directory.Delete(LogDir, true);
                Directory.CreateDirectory(LogDir);

                // On Unix the run-as account exists before the service does, and launchd
                // starts the job as soon as it is bootstrapped, so hand over the dir first.
                // On Windows the virtual account only exists once the service is created.
                if (!IsWindows()) await ApplyDirOwnership();

                if (IsWindows())
                {
                    string binPath = Path.Combine(AppContext.BaseDirectory, "Services", "ValarService.exe");
                    // Run under the auto-managed per-service virtual account instead of LocalSystem.
                    await Utils.ExecCmd($"sc create \"{WinService}\" binPath= \"{binPath}\" obj= \"{WinAccount}\" start= delayed-auto");
                    // The wrapper exits non-zero when the daemon dies; have SCM restart it.
                    await Utils.ExecCmd($"sc failure \"{WinService}\" reset= 86400 actions= restart/30000/restart/30000/restart/30000");
                    await ApplyDirOwnership();
                }
                else if (IsLinux())
                {
                    string servicePath = Path.Combine(AppContext.BaseDirectory, "Templates", "valar.service");
                    await Utils.ExecCmd($"cp {servicePath} /lib/systemd/system");
                    await Utils.ExecCmd("systemctl daemon-reload");
                    await Utils.ExecCmd("systemctl enable valar");
                }
                else if (IsMacOS())
                {
                    string plistPath = Path.Combine(AppContext.BaseDirectory, "Templates", "func.valar.plist");
                    await Utils.ExecCmd($"cp {plistPath} /Library/LaunchDaemons");
                    await Utils.ExecCmd("launchctl bootstrap system /Library/LaunchDaemons/func.valar.plist");
                }

                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // POST: valar/update
        [HttpPost("update")]
        public async Task<ActionResult> UpdateValar()
        {
            try
            {
                await StopValarService();
                await EnsureUv();
                await InstallDaemon(upgrade: true);
                await ApplyDirOwnership();
                await StartValarService();
                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // PUT: valar/start
        [HttpPut("start")]
        public async Task<ActionResult> StartValarService()
        {
            try
            {
                if (IsWindows())
                {
                    await Utils.ExecCmd($"sc start \"{WinService}\"");
                }
                else if (IsLinux())
                {
                    await Utils.ExecCmd("systemctl start valar");
                    await Utils.ExecCmd("systemctl daemon-reload");
                }
                else if (IsMacOS())
                {
                    await Utils.ExecCmd("launchctl bootstrap system /Library/LaunchDaemons/func.valar.plist");
                }

                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // PUT: valar/stop
        [HttpPut("stop")]
        public async Task<ActionResult> StopValarService()
        {
            try
            {
                if (IsWindows())
                {
                    await Utils.ExecCmd($"sc stop \"{WinService}\"");
                }
                else if (IsLinux())
                {
                    await Utils.ExecCmd("systemctl stop valar");
                    await Utils.ExecCmd("systemctl daemon-reload");
                }
                else if (IsMacOS())
                {
                    await Utils.ExecCmd("launchctl bootout system/func.valar");
                }

                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // DELETE: valar
        [HttpDelete]
        public async Task<ActionResult> DeleteValarService()
        {
            try
            {
                if (IsWindows())
                {
                    await Utils.ExecCmd($"sc delete \"{WinService}\"");
                }
                else if (IsLinux())
                {
                    await Utils.ExecCmd("rm /lib/systemd/system/valar.service");
                    await Utils.ExecCmd("systemctl daemon-reload");
                }
                else if (IsMacOS())
                {
                    await Utils.ExecCmd("launchctl bootout system/func.valar");
                    await Utils.ExecCmd("rm /Library/LaunchDaemons/func.valar.plist");
                }

                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // Environment for every uv invocation. Everything stays inside the valar dir and
        // nothing depends on HOME, the user cache, shims in ~/.local/bin or the Windows
        // registry, all of which would otherwise be touched as root/SYSTEM.
        private static Dictionary<string, string> UvEnv() => new()
        {
            ["UV_PYTHON_INSTALL_DIR"] = PythonDir,
            ["UV_MANAGED_PYTHON"] = "1",
            ["UV_PYTHON_INSTALL_BIN"] = "0",
            ["UV_PYTHON_INSTALL_REGISTRY"] = "0",
            ["UV_NO_CACHE"] = "1",
            ["UV_LINK_MODE"] = "copy",
            ["UV_NO_CONFIG"] = "1",
            ["UV_NO_PROGRESS"] = "1",
        };

        private static async Task RunUv(params string[] args)
        {
            var result = await Utils.RunProcess(UvPath, args, UvEnv(), ValarDir);
            if (result.ExitCode != 0)
            {
                // uv reports its failures on stderr; surface that to the UI.
                throw new Exception($"uv {args[0]} failed: {result.Stderr.Trim()}");
            }
        }

        private static async Task InstallDaemon(bool upgrade)
        {
            // No-op when the requested Python is already installed.
            await RunUv("python", "install", PythonVersion);

            if (!upgrade || !System.IO.File.Exists(VenvPython))
            {
                if (Directory.Exists(VenvDir)) Directory.Delete(VenvDir, true);
                await RunUv("venv", VenvDir, "--python", PythonVersion);
            }

            var pipArgs = new List<string> { "pip", "install", "--python", VenvPython };
            if (upgrade) pipArgs.Add("--upgrade");
            pipArgs.Add(PackageName);
            await RunUv([.. pipArgs]);

            if (InstalledVersion() == null)
            {
                throw new Exception($"Failed to install {PackageName}");
            }
        }

        // Download the uv binary from GitHub Releases (sha256-verified) unless a working
        // copy is already present.
        private static async Task EnsureUv()
        {
            if (System.IO.File.Exists(UvPath))
            {
                try
                {
                    var check = await Utils.RunProcess(UvPath, ["--version"], UvEnv(), ValarDir);
                    if (check.ExitCode == 0) return;
                }
                catch { }
            }

            var client = new GitHubClient(new ProductHeaderValue("func"));
            var latest = await client.Repository.Release.GetLatest("astral-sh", "uv");

            string arch = RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "aarch64" : "x86_64";
            string assetName = (IsWindows() ? $"uv-{arch}-pc-windows-msvc.zip"
                : IsLinux() ? $"uv-{arch}-unknown-linux-gnu.tar.gz"
                : IsMacOS() ? $"uv-{arch}-apple-darwin.tar.gz"
                : null) ?? throw new Exception("Binary Not Found");
            var asset = latest.Assets.FirstOrDefault(a => a.Name == assetName) ?? throw new Exception("Binary Not Found");
            var shaAsset = latest.Assets.FirstOrDefault(a => a.Name == assetName + ".sha256");

            Directory.CreateDirectory(ValarDir);
            string filePath = Path.Combine(Utils.appDataDir, asset.Name);
            using var httpClient = new HttpClient();
            using (var s = await httpClient.GetStreamAsync(asset.BrowserDownloadUrl))
            using (FileStream fs = new(filePath, System.IO.FileMode.Create))
            {
                await s.CopyToAsync(fs);
            }

            if (shaAsset != null)
            {
                // Sidecar format: "<hex>  <filename>"
                string expected = (await httpClient.GetStringAsync(shaAsset.BrowserDownloadUrl)).Trim().Split(' ', '\t')[0];
                string actual;
                using (FileStream fs = new(filePath, System.IO.FileMode.Open, FileAccess.Read))
                {
                    actual = Convert.ToHexString(await SHA256.HashDataAsync(fs));
                }
                if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
                {
                    System.IO.File.Delete(filePath);
                    throw new Exception("uv download failed checksum verification");
                }
            }

            if (IsWindows())
            {
                // uv.exe sits at the zip root alongside uvx.exe/uvw.exe, which we don't need.
                ZipFile.ExtractToDirectory(filePath, ValarDir, true);
                foreach (string extra in new[] { "uvx.exe", "uvw.exe" })
                {
                    string path = Path.Combine(ValarDir, extra);
                    if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
                }
            }
            else
            {
                // The tarball holds a uv-<triple>/ folder; pull the binary out of it.
                string tmpDir = Path.Combine(ValarDir, "uv-extract");
                if (Directory.Exists(tmpDir)) Directory.Delete(tmpDir, true);
                Directory.CreateDirectory(tmpDir);
                using (FileStream rfs = new(filePath, System.IO.FileMode.Open, FileAccess.Read))
                using (GZipStream gz = new(rfs, CompressionMode.Decompress))
                {
                    await TarFile.ExtractToDirectoryAsync(gz, tmpDir, true);
                }
                string extracted = Directory.GetFiles(tmpDir, "uv", SearchOption.AllDirectories).FirstOrDefault()
                    ?? throw new Exception("uv binary not found in archive");
                System.IO.File.Move(extracted, UvPath, true);
                Directory.Delete(tmpDir, true);
                await Utils.ExecCmd($"chmod 755 '{UvPath}'");
            }

            System.IO.File.Delete(filePath);
        }
    }
}
