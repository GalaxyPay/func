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
                    string workspaceName = "algorandfoundation";
                    string repositoryName = "reti";
                    var client = new GitHubClient(new ProductHeaderValue(repositoryName));
                    var latest = await client.Repository.Release.GetLatest(workspaceName, repositoryName);

                    Directory.CreateDirectory(Path.Combine(Utils.dataPath, "reti"));

                    if (IsWindows())
                    {
                        var url = latest.Assets.FirstOrDefault(a => a.Name.EndsWith("windows-amd64.zip"))?.BrowserDownloadUrl;
                        if (url == null) return BadRequest();
                        await Utils.ExecCmd($"curl -L -o {Utils.dataPath}/reti.zip {url}");
                        await Utils.ExecCmd($"tar -xf {Utils.dataPath}/reti.zip -C {Path.Combine(Utils.dataPath, "reti")}");
                    }
                    else if (IsLinux())
                    {
                        var url = latest.Assets.FirstOrDefault(a => a.Name.Contains("linux-amd64.tar.gz"))?.BrowserDownloadUrl;
                        if (url == null) return BadRequest();
                        await Utils.ExecCmd($"curl -L -o {Utils.dataPath}/reti.tar.gz {url}");
                        await Utils.ExecCmd($"tar -zxf {Utils.dataPath}/reti.tar.gz -C {Path.Combine(Utils.dataPath, "reti")}");
                    }
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

                string workspaceName = "algorandfoundation";
                string repositoryName = "reti";
                var client = new GitHubClient(new ProductHeaderValue(repositoryName));

                var latest = await client.Repository.Release.GetLatest(workspaceName, repositoryName);

                Directory.CreateDirectory(Path.Combine(Utils.dataPath, "reti"));

                if (IsWindows())
                {
                    var url = latest.Assets.FirstOrDefault(a => a.Name.EndsWith("windows-amd64.zip"))?.BrowserDownloadUrl;
                    if (url == null) return BadRequest();
                    await Utils.ExecCmd($"curl -L -o {Utils.dataPath}/reti.zip {url}");
                    await Utils.ExecCmd($"tar -xf {Utils.dataPath}/reti.zip -C {Utils.dataPath} reti");
                }
                else if (IsLinux())
                {
                    var url = latest.Assets.FirstOrDefault(a => a.Name.Contains("linux-amd64.tar.gz"))?.BrowserDownloadUrl;
                    if (url == null) return BadRequest();
                    await Utils.ExecCmd($"curl -L -o {Utils.dataPath}/reti.tar.gz {url}");
                    await Utils.ExecCmd($"tar -zxf {Utils.dataPath}/reti.tar.gz -C {Utils.dataPath} reti");
                }

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

                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}
