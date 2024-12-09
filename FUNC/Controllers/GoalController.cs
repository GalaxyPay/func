using System.Runtime.InteropServices;
using FUNC.Models;
using Microsoft.AspNetCore.Mvc;
using Octokit;
using static System.Environment;
using static System.OperatingSystem;

namespace FUNC.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class GoalController(ILogger<GoalController> logger) : ControllerBase
    {
        private readonly ILogger<GoalController> _logger = logger;

        // GET: goal/version
        [HttpGet("version")]
        public async Task<ActionResult<GoalVersion>> GoalVersion()
        {
            try
            {
                string installed = string.Empty;
                string latest = string.Empty;

                string goalPath = Path.Combine(Utils.dataPath, "bin", "goal");
                string version = await Utils.ExecCmd($"{goalPath} --version");
                if (version != string.Empty)
                {
                    int firstBreak = version.IndexOf("\n") + 1;
                    string secondLine = version[firstBreak..version.IndexOf("\n", firstBreak)];
                    installed = secondLine[..secondLine.LastIndexOf(".")];
                }

                string workspaceName = string.Empty;
                string repositoryName = string.Empty;

                if (IsWindows())
                {
                    workspaceName = "GalaxyPay";
                    repositoryName = "go-algo-win";
                }
                else if (IsLinux())
                {
                    workspaceName = "algorand";
                    repositoryName = "go-algorand";
                }

                var client = new GitHubClient(new ProductHeaderValue(repositoryName));
                var latestInfo = await client.Repository.Release.GetLatest(workspaceName, repositoryName);
                latest = latestInfo.TagName[1..latestInfo.TagName.IndexOf("-")];

                return new GoalVersion() { Installed = installed, Latest = latest };
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // POST: goal/update
        [HttpPost("update")]
        public async Task<ActionResult<string>> GoalUpdate(Models.Release model)
        {
            try
            {
                if (IsWindows())
                {
                    string workspaceName = "GalaxyPay";
                    string repositoryName = "go-algo-win";
                    var client = new GitHubClient(new ProductHeaderValue(repositoryName));

                    Octokit.Release? release = null;
                    if (model.Name == "latest")
                    {
                        release = await client.Repository.Release.GetLatest(workspaceName, repositoryName);
                    }
                    else
                    {
                        release = await client.Repository.Release.Get(workspaceName, repositoryName, model.Name);
                    }
                    var url = release?.Assets.FirstOrDefault(a => a.Name == "node.tar.gz")?.BrowserDownloadUrl;
                    if (url == null) return BadRequest();
                    await Utils.ExecCmd($"curl -L -o {Utils.dataPath}/node.tar.gz {url}");
                }
                else if (IsLinux())
                {
                    if (model.Name != "latest") return BadRequest("Custom versions not supported on Linux");

                    string workspaceName = "algorand";
                    string repositoryName = "go-algorand";

                    var client = new GitHubClient(new ProductHeaderValue(repositoryName));
                    var latestInfo = await client.Repository.Release.GetLatest(workspaceName, repositoryName);

                    string? url = null;
                    if (RuntimeInformation.OSArchitecture == Architecture.Arm64)
                    {
                        url = latestInfo.Assets.FirstOrDefault(a => a.Name.Contains("node_stable_linux-arm64")
                        && a.Name.EndsWith("tar.gz"))?.BrowserDownloadUrl;
                    }
                    else
                    {
                        url = latestInfo.Assets.FirstOrDefault(a => a.Name.Contains("node_stable_linux-amd64")
                           && a.Name.EndsWith("tar.gz"))?.BrowserDownloadUrl;
                    }
                    if (url == null) return BadRequest();
                    await Utils.ExecCmd($"wget -L -O {Utils.dataPath}/node.tar.gz {url}");
                }

                await Utils.ExecCmd($"tar -zxf {Utils.dataPath}/node.tar.gz -C {Utils.dataPath} bin");
                await Utils.ExecCmd($"rm {Utils.dataPath}/node.tar.gz");

                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}
