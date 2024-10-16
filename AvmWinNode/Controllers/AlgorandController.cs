using AvmWinNode.Models;
using Microsoft.AspNetCore.Mvc;
using static System.Environment;

namespace AvmWinNode.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class AlgorandController(ILogger<AlgorandController> logger) : ControllerBase
    {

        private readonly ILogger<AlgorandController> _logger = logger;
        private readonly string _dataPath = Path.Combine(GetFolderPath(SpecialFolder.CommonApplicationData), @"AvmWinNode\");

        // GET: algorand
        [HttpGet]
        public ActionResult<NodeConfig> GetAlgorand()
        {
            try
            {
                string net = ":0";
                try { net = System.IO.File.ReadAllText(_dataPath + @"algorand\algod.net").Replace("\n", ""); } catch { }
                int port = int.Parse(net[(net.LastIndexOf(":") + 1)..]);
                string token = string.Empty;
                try { token = System.IO.File.ReadAllText(_dataPath + @"algorand\algod.admin.token"); } catch { }
                string sc = Utils.ExecCmd(@"sc query ""Algorand Node""");
                string serviceStatus = Utils.ParseServiceStatus(sc);
                NodeConfig config = new()
                {
                    Port = port,
                    Token = token,
                    ServiceStatus = serviceStatus
                };
                return config;
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // POST: algorand
        [HttpPost]
        public ActionResult<string> CreateAlgorandService()
        {
            try
            {
                if (!Directory.Exists(_dataPath + "algorand"))
                {
                    Utils.ExecCmd(@"tar -xf """ + AppContext.BaseDirectory + @"Templates\algorand.zip"" -C " + _dataPath);
                }
                return Utils.ExecCmd(@"sc create ""Algorand Node"" binPath= """ + AppContext.BaseDirectory + @"Services\AlgorandService.exe"" start= auto");
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // POST: algorand/reset
        [HttpPost("reset")]
        public ActionResult<string> ResetAlgorandNode()
        {
            try
            {
                Directory.Delete(_dataPath + "algorand", true);
                Utils.ExecCmd(@"tar -xf """ + AppContext.BaseDirectory + @"Templates\algorand.zip"" -C " + _dataPath);
                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // POST: algorand/catchup
        [HttpPost("catchup")]
        public ActionResult<string> CatchupAlgorandNode(Catchup model)
        {
            try
            {
                var round = model.Catchpoint.Split('#')[0];
                var data = model.Catchpoint.Split('#')[1];
                if (string.IsNullOrEmpty(model.Catchpoint)
                    || model.Catchpoint.Any(Char.IsWhiteSpace)
                    || !int.TryParse(round, out _)
                    || data.Length != 52)
                    return BadRequest();
                string cmd = string.Format(_dataPath + "goal node catchup {0} -d " + _dataPath + "algorand", model.Catchpoint);
                return Utils.ExecCmd(cmd);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // PUT: algorand/start
        [HttpPut("start")]
        public ActionResult<string> StartAlgorandService()
        {
            try
            {
                return Utils.ExecCmd(@"sc start ""Algorand Node""");
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // PUT: algorand/stop
        [HttpPut("stop")]
        public ActionResult<string> StopAlgorandService()
        {
            try
            {
                return Utils.ExecCmd(@"sc stop ""Algorand Node""");
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // DELETE: algorand
        [HttpDelete]
        public ActionResult<string> DeleteAlgorandService()
        {
            try
            {
                return Utils.ExecCmd(@"sc delete ""Algorand Node""");
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}
