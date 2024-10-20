using Microsoft.AspNetCore.Mvc;
using static System.Environment;

namespace AvmWinNode.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class RetiController(ILogger<RetiController> logger) : ControllerBase
    {

        private readonly ILogger<RetiController> _logger = logger;
        private readonly string _dataPath = Path.Combine(GetFolderPath(SpecialFolder.CommonApplicationData), @"AvmWinNode\");
        private readonly string _releasePath = "https://github.com/TxnLab/reti/releases/latest/download/";

        // GET: reti/version
        [HttpGet("version")]
        public ActionResult<string> GoalVersion(string latest)
        {
            try
            {
                string reti = _dataPath + @"reti\reti.exe";
                if (!System.IO.File.Exists(reti))
                {
                    string path = _releasePath + "reti-" + latest + "-windows-amd64.zip";
                    Utils.ExecCmd("curl -sL -o " + _dataPath + "reti.zip " + path);
                    var test = Utils.ExecCmd(@"tar -xf " + _dataPath + "reti.zip -C " + _dataPath + "reti");
                }
                string output = Utils.ExecCmd(reti + " --version");
                return output;
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}
