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
        private readonly string _releasePath = "https://github.com/GalaxyPay/algowin/releases/latest/download/";

        // GET: goal/version
        [HttpGet("version")]
        public ActionResult<string> GoalVersion()
        {
            try
            {
                return Utils.ExecCmd(_dataPath + "goal --version");
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // POST: goal/update
        [HttpPost("update")]
        public ActionResult<string> GoalUpdate()
        {
            try
            {
                Utils.ExecCmd("curl -sL -o " + _dataPath + "algod.exe " + _releasePath + "algod.exe");
                Utils.ExecCmd("curl -sL -o " + _dataPath + "goal.exe " + _releasePath + "goal.exe");
                Utils.ExecCmd("curl -sL -o " + _dataPath + "kmd.exe " + _releasePath + "kmd.exe");
                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}
