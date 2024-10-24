using AvmWinNode.Models;
using Microsoft.AspNetCore.Mvc;
using System.Dynamic;
using System.Text.Json;
using static System.Environment;

namespace AvmWinNode.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class RetiController(ILogger<RetiController> logger) : ControllerBase
    {

        private readonly ILogger<RetiController> _logger = logger;
        private readonly string _dataPath = Path.Combine(GetFolderPath(SpecialFolder.CommonApplicationData), @"AvmWinNode\");
        private readonly string _releasePath = "https://github.com/algorandfoundation/reti/releases/latest/download/";

        // POST: reti
        [HttpPost]
        public async Task<ActionResult<string>> CreateRetiService(RetiCreate model)
        {
            try
            {
                var latestString = await Utils.ExecCmd("curl https://api.github.com/repos/algorandfoundation/reti/releases/latest");
                dynamic? latest = JsonSerializer.Deserialize<ExpandoObject>(latestString);

                string exePath = _dataPath + @"reti\reti.exe";
                if (!System.IO.File.Exists(exePath))
                {
                    string zipPath = _releasePath + "reti-" + latest?.name + "-windows-amd64.zip";
                    await Utils.ExecCmd("curl -sL -o " + _dataPath + "reti.zip " + zipPath);
                    await Utils.ExecCmd(@"tar -xf " + _dataPath + "reti.zip -C " + _dataPath + "reti");
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

                var latestString = await Utils.ExecCmd("curl https://api.github.com/repos/algorandfoundation/reti/releases/latest");
                dynamic? latest = JsonSerializer.Deserialize<ExpandoObject>(latestString);

                string exePath = _dataPath + @"reti\reti.exe";
                string zipPath = _releasePath + "reti-" + latest?.name + "-windows-amd64.zip";
                await Utils.ExecCmd("curl -sL -o " + _dataPath + "reti.zip " + zipPath);
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
