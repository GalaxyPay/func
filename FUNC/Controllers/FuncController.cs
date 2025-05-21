using System.Runtime.InteropServices;
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

        // GET: func/latest
        [HttpGet("latest")]
        public async Task<ActionResult<string>> FuncLatest()
        {
            try
            {
                string workspaceName = "GalaxyPay";
                string repositoryName = "func";
                var client = new GitHubClient(new ProductHeaderValue(repositoryName));
                var latestInfo = await client.Repository.Release.GetLatest(workspaceName, repositoryName);

                string? pattern = IsWindows() ? (RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "windows-arm64" : "windows-amd64")
                : IsLinux() ? (RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "linux-arm64" : "linux-amd64")
                : IsMacOS() ? (RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "darwin-arm64" : "darwin-amd64")
                : null;

                if (pattern == null) return BadRequest();
                var asset = latestInfo?.Assets.FirstOrDefault(a => a.Name.Contains(pattern));
                if (asset == null) return BadRequest();
                return asset.BrowserDownloadUrl;
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}
