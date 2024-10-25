using AvmWinNode.Models;
using Microsoft.AspNetCore.Mvc;
using Octokit;
using static System.Environment;

namespace AvmWinNode.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class RetiController(ILogger<RetiController> logger) : ControllerBase
    {

        private readonly ILogger<RetiController> _logger = logger;
        private readonly string _dataPath = Path.Combine(GetFolderPath(SpecialFolder.CommonApplicationData), @"AvmWinNode\");

        // POST: reti
        [HttpPost]
        public async Task<ActionResult<string>> CreateRetiService(RetiCreate model)
        {
            try
            {
                string exePath = _dataPath + @"reti\reti.exe";
                if (!System.IO.File.Exists(exePath))
                {
                    string workspaceName = "algorandfoundation";
                    string repositoryName = "reti";
                    var client = new GitHubClient(new ProductHeaderValue(repositoryName));

                    var latest = await client.Repository.Release.GetLatest(workspaceName, repositoryName);
                    var url = latest.Assets.FirstOrDefault(a => a.Name.Contains("windows-amd64"))?.BrowserDownloadUrl;
                    if (url == null) return BadRequest();

                    Directory.CreateDirectory(_dataPath + "reti");
                    await Utils.ExecCmd("curl -sL -o " + _dataPath + "reti.zip " + url);
                    await Utils.ExecCmd(@"tar -xf " + _dataPath + "reti.zip -C " + _dataPath + "reti");
                }
                if (!System.IO.File.Exists(exePath))
                {
                    throw new Exception("Failed to download reti.exe");
                }

                string envPath = _dataPath + @"reti\.env";
                if (System.IO.File.Exists(envPath))
                {
                    System.IO.File.Delete(envPath);
                }
                using (StreamWriter sw = System.IO.File.CreateText(envPath))
                {
                    sw.WriteLine(model.Env);
                }
                return await Utils.ExecCmd(@"sc create ""Reti Validator"" binPath= """ + AppContext.BaseDirectory + @"Services\RetiService.exe"" start= delayed-auto");
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
                var url = latest.Assets.FirstOrDefault(a => a.Name.Contains("windows-amd64"))?.BrowserDownloadUrl;
                if (url == null) return BadRequest();

                Directory.CreateDirectory(_dataPath + "reti");
                await Utils.ExecCmd("curl -sL -o " + _dataPath + "reti.zip " + url);
                await Utils.ExecCmd(@"tar -xf " + _dataPath + "reti.zip -C " + _dataPath + "reti");

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
        public async Task<ActionResult<string>> StartRetiService()
        {
            try
            {
                return await Utils.ExecCmd(@"sc start ""Reti Validator""");
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
                return await Utils.ExecCmd(@"sc stop ""Reti Validator""");
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
                return await Utils.ExecCmd(@"sc delete ""Reti Validator""");
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}
