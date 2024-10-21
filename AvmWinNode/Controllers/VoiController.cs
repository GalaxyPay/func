using AvmWinNode.Models;
using Microsoft.AspNetCore.Mvc;
using static System.Environment;

namespace AvmWinNode.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class VoiController(ILogger<VoiController> logger) : ControllerBase
    {

        private readonly ILogger<VoiController> _logger = logger;
        private readonly string _dataPath = Path.Combine(GetFolderPath(SpecialFolder.CommonApplicationData), @"AvmWinNode\");

        // GET: voi
        [HttpGet]
        public ActionResult<NodeConfig> GetVoi()
        {
            try
            {
                string net = ":0";
                try { net = System.IO.File.ReadAllText(_dataPath + @"voi\algod.net").Replace("\n", ""); } catch { }
                int port = int.Parse(net[(net.LastIndexOf(":") + 1)..]);
                string token = string.Empty;
                try { token = System.IO.File.ReadAllText(_dataPath + @"voi\algod.admin.token"); } catch { }
                string sc = Utils.ExecCmd(@"sc query ""Voi Node""");
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

        // POST: voi
        [HttpPost]
        public ActionResult<string> CreateVoiService()
        {
            try
            {
                if (!Directory.Exists(_dataPath + "voi"))
                {
                    Utils.ExecCmd(@"tar -xf """ + AppContext.BaseDirectory + @"Templates\voi.zip"" -C " + _dataPath);
                }
                string binPath = @"""\""" + AppContext.BaseDirectory + @"Services\NodeService.exe\"" voi""";
                return Utils.ExecCmd(@"sc create ""Voi Node"" binPath= " + binPath + @" start= auto");
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // POST: voi/reset
        [HttpPost("reset")]
        public ActionResult<string> ResetVoiNode()
        {
            try
            {
                Directory.Delete(_dataPath + "voi", true);
                Utils.ExecCmd(@"tar -xf """ + AppContext.BaseDirectory + @"Templates\voi.zip"" -C " + _dataPath);
                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // POST: voi/catchup
        [HttpPost("catchup")]
        public ActionResult<string> CatchupVoiNode(Catchup model)
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
                string cmd = string.Format(_dataPath + "goal node catchup {0} -d " + _dataPath + "voi", model.Catchpoint);
                return Utils.ExecCmd(cmd);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // PUT: voi/start
        [HttpPut("start")]
        public ActionResult<string> StartVoiService()
        {
            try
            {
                return Utils.ExecCmd(@"sc start ""Voi Node""");
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // PUT: voi/stop
        [HttpPut("stop")]
        public ActionResult<string> StopVoiService()
        {
            try
            {
                return Utils.ExecCmd(@"sc stop ""Voi Node""");
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // DELETE: voi
        [HttpDelete]
        public ActionResult<string> DeleteVoiService()
        {
            try
            {
                return Utils.ExecCmd(@"sc delete ""Voi Node""");
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}
