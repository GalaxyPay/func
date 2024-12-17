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

        // POST: reti
        [HttpPost]
        public async Task<ActionResult> CreateRetiService(RetiCreate model)
        {
            try
            {
                string exePath = Path.Combine(Utils.dataPath, "reti", "reti");
                if (IsWindows()) exePath += ".exe";

                if (!System.IO.File.Exists(exePath))
                {
                    await DownloadExtractReti();
                }

                if (!System.IO.File.Exists(exePath))
                {
                    throw new Exception("Failed to download Reti");
                }

                string envPath = Path.Combine(Utils.dataPath, "reti", ".env");
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
                    await Utils.ExecCmd($"sc create \"Reti Validator\" binPath= \"{binPath}\" start= delayed-auto");
                }
                else if (IsLinux())
                {
                    string servicePath = Path.Combine(AppContext.BaseDirectory, "Templates", "reti.service");
                    await Utils.ExecCmd($"cp {servicePath} /lib/systemd/system");
                    await Utils.ExecCmd($"systemctl daemon-reload");
                    await Utils.ExecCmd($"systemctl enable reti");
                }
                else if (IsMacOS())
                {
                    string plistPath = Path.Combine(AppContext.BaseDirectory, "Templates", $"func.reti.plist");
                    await Utils.ExecCmd($"cp {plistPath} /Library/LaunchDaemons");
                    await Utils.ExecCmd($"launchctl bootstrap system /Library/LaunchDaemons/func.reti.plist");
                }

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
                    await Utils.ExecCmd($"launchctl kickstart system/func.reti");
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
        public async Task<ActionResult<string>> StopRetiService()
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
                    await Utils.ExecCmd($"launchctl kill 9 system/func.reti");
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
        public async Task<ActionResult<string>> DeleteRetiService()
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

            Directory.CreateDirectory(Path.Combine(Utils.dataPath, "reti"));

            if (IsWindows())
            {
                var url = (latest.Assets.FirstOrDefault(a => a.Name.EndsWith("windows-amd64.zip"))?.BrowserDownloadUrl)
                    ?? throw new Exception("Binary Not Found");
                await Utils.ExecCmd($"curl -L -o {Utils.dataPath}/reti.zip {url}");
                await Utils.ExecCmd($"tar -xf {Utils.dataPath}/reti.zip -C {Path.Combine(Utils.dataPath, "reti")}");
            }
            else if (IsLinux())
            {
                string? url = null;
                if (RuntimeInformation.OSArchitecture == Architecture.Arm64)
                {
                    url = latest.Assets.FirstOrDefault(a => a.Name.EndsWith("linux-arm64.tar.gz"))?.BrowserDownloadUrl
                        ?? throw new Exception("Binary Not Found");
                }
                else
                {
                    url = latest.Assets.FirstOrDefault(a => a.Name.EndsWith("linux-amd64.tar.gz"))?.BrowserDownloadUrl
                        ?? throw new Exception("Binary Not Found");
                }
                await Utils.ExecCmd($"wget -L -O {Utils.dataPath}/reti.tar.gz {url}");
                await Utils.ExecCmd($"tar -zxf {Utils.dataPath}/reti.tar.gz -C {Path.Combine(Utils.dataPath, "reti")}");
            }
            else if (IsMacOS())
            {
                string? url = null;
                if (RuntimeInformation.OSArchitecture == Architecture.Arm64)
                {
                    url = latest.Assets.FirstOrDefault(a => a.Name.EndsWith("darwin-arm64.tar.gz"))?.BrowserDownloadUrl
                        ?? throw new Exception("Binary Not Found");
                }
                else
                {
                    url = latest.Assets.FirstOrDefault(a => a.Name.EndsWith("darwin-amd64.tar.gz"))?.BrowserDownloadUrl
                        ?? throw new Exception("Binary Not Found");
                }
                await Utils.ExecCmd($"curl -L -o {Utils.dataPath}/reti.tar.gz {url}");
                await Utils.ExecCmd($"tar -zxf {Utils.dataPath}/reti.tar.gz -C {Path.Combine(Utils.dataPath, "reti")}");
            }
        }
    }
}
