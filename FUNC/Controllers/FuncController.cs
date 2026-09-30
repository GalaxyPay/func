using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc;
using Octokit;
using static System.OperatingSystem;

namespace FUNC.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class FuncController(ILogger<FuncController> logger) : ControllerBase
    {
        private readonly ILogger<FuncController> _logger = logger;

        private static async Task<string> DownloadLatestInstaller(string pattern)
        {
            var client = new GitHubClient(new ProductHeaderValue("func"));
            var latest = await client.Repository.Release.GetLatest("GalaxyPay", "func");
            var asset = latest.Assets.FirstOrDefault(a => a.Name.EndsWith(pattern))
                ?? throw new Exception("Installer Not Found");
            var shaAsset = latest.Assets.FirstOrDefault(a => a.Name == asset.Name + ".sha256")
                ?? throw new Exception("Installer Checksum Not Found");

            // The installer kills this process mid-install, so the download can't
            // be deleted afterward; clear downloads left by previous updates
            // (loose files from older versions, private dirs from newer ones).
            string tempPath = Path.GetTempPath();
            foreach (string old in Directory.GetFiles(tempPath, $"func_*{pattern}"))
            {
                try { System.IO.File.Delete(old); } catch { }
            }
            foreach (string old in Directory.GetDirectories(tempPath, "func-update-*"))
            {
                try { Directory.Delete(old, true); } catch { }
            }

            // The installer runs as root/SYSTEM, so download it into a fresh directory
            // only this account can write to (0700 on Unix), not a predictable path
            // in the shared temp dir.
            string dir = Directory.CreateTempSubdirectory("func-update-").FullName;
            string filePath = Path.Combine(dir, asset.Name);
            using var httpClient = new HttpClient();
            using (var s = await httpClient.GetStreamAsync(asset.BrowserDownloadUrl))
            using (FileStream fs = new(filePath, System.IO.FileMode.CreateNew))
            {
                await s.CopyToAsync(fs);
            }

            // Sidecar format: "<hex>  <filename>"
            string expected = (await httpClient.GetStringAsync(shaAsset.BrowserDownloadUrl)).Trim().Split(' ', '\t')[0];
            string actual;
            using (FileStream fs = new(filePath, System.IO.FileMode.Open, FileAccess.Read))
            {
                actual = Convert.ToHexString(await SHA256.HashDataAsync(fs));
            }
            if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
            {
                Directory.Delete(dir, true);
                throw new Exception("Installer failed checksum verification");
            }
            return filePath;
        }

        // GET: func/started
        // Process start time; the client uses a change in this value to detect
        // that the service was restarted (i.e. an update completed).
        [HttpGet("started")]
        public ActionResult<string> FuncStarted()
        {
            using var p = Process.GetCurrentProcess();
            return p.StartTime.ToUniversalTime().ToString("O");
        }

        // POST: func/update
        [HttpPost("update")]
        public async Task<ActionResult> FuncUpdate()
        {
            try
            {
                string arch = RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "arm64" : "amd64";
                if (IsWindows())
                {
                    string installerPath = await DownloadLatestInstaller($"_windows-{arch}.exe");
                    // Run the installer in a detached process; children of a
                    // service survive the installer stopping the FUNC service.
                    ProcessStartInfo psi = new()
                    {
                        FileName = installerPath,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                    };
                    psi.ArgumentList.Add("/VERYSILENT");
                    psi.ArgumentList.Add("/SUPPRESSMSGBOXES");
                    psi.ArgumentList.Add("/NORESTART");
                    Process.Start(psi);
                }
                else if (IsLinux())
                {
                    string installerPath = await DownloadLatestInstaller($"_linux-{arch}.deb");
                    // systemd-run puts the upgrade in its own transient unit so it
                    // isn't killed when the package's prerm stops the func service
                    // (systemd kills the service's whole cgroup).
                    ProcessStartInfo psi = new()
                    {
                        FileName = "systemd-run",
                        UseShellExecute = false,
                    };
                    psi.ArgumentList.Add("--collect");
                    psi.ArgumentList.Add("/usr/bin/dpkg");
                    psi.ArgumentList.Add("-i");
                    psi.ArgumentList.Add(installerPath);
                    Process.Start(psi);
                }
                else if (IsMacOS())
                {
                    string installerPath = await DownloadLatestInstaller($"_darwin-{arch}.pkg");
                    // Run the upgrade as its own launchd job so it isn't killed when
                    // the pkg's preinstall boots out func.api (launchd kills the
                    // job's process group).
                    string templatePath = Path.Combine(AppContext.BaseDirectory, "Templates", "func.update.plist");
                    string plist = System.IO.File.ReadAllText(templatePath).Replace("__PKG__", installerPath);
                    System.IO.File.WriteAllText("/Library/LaunchDaemons/func.update.plist", plist);
                    await Utils.Launchctl("bootout", "system/func.update"); // clear previous run, if any
                    await Utils.Launchctl("bootstrap", "system", "/Library/LaunchDaemons/func.update.plist");
                }
                else return BadRequest();
                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}
