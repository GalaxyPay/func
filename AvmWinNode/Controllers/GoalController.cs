using AvmWinNode.Models;
using Microsoft.AspNetCore.Mvc;
using static System.Environment;

namespace AvmWinNode.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class GoalController(ILogger<GoalController> logger) : ControllerBase
    {

        private readonly ILogger<GoalController> _logger = logger;
        private readonly string _dataPath = Path.Combine(GetFolderPath(SpecialFolder.CommonApplicationData), @"AvmWinNode\");

        // GET: goal/version
        [HttpGet("version")]
        public async Task<ActionResult<string>> GoalVersion()
        {
            try
            {
                return await Utils.ExecCmd($"{_dataPath}goal --version");
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // POST: goal/update
        [HttpPost("update")]
        public async Task<ActionResult<string>> GoalUpdate(Release model)
        {
            try
            {
                string releasePath = "https://github.com/GalaxyPay/algowin/releases/";

                if (model.Name == "latest")
                {
                    releasePath += "latest/download/";
                }
                else
                {
                    releasePath += $"download/{model.Name}/";
                }

                await Utils.ExecCmd($"curl -sL -o {_dataPath}algod.exe {releasePath}algod.exe");
                await Utils.ExecCmd($"curl -sL -o {_dataPath}goal.exe {releasePath}goal.exe");
                await Utils.ExecCmd($"curl -sL -o {_dataPath}kmd.exe {releasePath}kmd.exe");

                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}
