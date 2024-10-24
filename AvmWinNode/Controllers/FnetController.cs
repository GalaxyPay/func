using AvmWinNode.Models;
using Microsoft.AspNetCore.Mvc;
using static System.Environment;

namespace AvmWinNode.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class FnetController(ILogger<FnetController> logger) : ControllerBase
    {

        private readonly ILogger<FnetController> _logger = logger;
        private readonly string _dataPath = Path.Combine(GetFolderPath(SpecialFolder.CommonApplicationData), @"AvmWinNode\");

        // GET: fnet
        [HttpGet]
        public ActionResult<NodeConfig> GetFnet()
        {
            try
            {
                string net = ":0";
                try { net = System.IO.File.ReadAllText(_dataPath + @"fnet\algod.net").Replace("\n", ""); } catch { }
                int port = int.Parse(net[(net.LastIndexOf(":") + 1)..]);
                string token = string.Empty;
                try { token = System.IO.File.ReadAllText(_dataPath + @"fnet\algod.admin.token"); } catch { }
                string sc = Utils.ExecCmd(@"sc query ""Fnet Node""");
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

        // POST: fnet
        [HttpPost]
        public ActionResult<string> CreateFnetService()
        {
            try
            {
                if (!Directory.Exists(_dataPath + "fnet"))
                {
                    Utils.ExecCmd(@"tar -xf """ + AppContext.BaseDirectory + @"Templates\fnet.zip"" -C " + _dataPath);
                }
                string binPath = @"""\""" + AppContext.BaseDirectory + @"Services\NodeService.exe\"" fnet""";
                return Utils.ExecCmd(@"sc create ""Fnet Node"" binPath= " + binPath + @" start= auto");
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // POST: fnet/reset
        [HttpPost("reset")]
        public ActionResult<string> ResetFnetNode()
        {
            try
            {
                if (Directory.Exists(_dataPath + "fnet"))
                {
                    Directory.Delete(_dataPath + "fnet", true);
                }
                Utils.ExecCmd(@"tar -xf """ + AppContext.BaseDirectory + @"Templates\fnet.zip"" -C " + _dataPath);
                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // POST: fnet/catchup
        [HttpPost("catchup")]
        public ActionResult<string> CatchupFnetNode(Catchup model)
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
                string cmd = string.Format(_dataPath + "goal node catchup {0} -d " + _dataPath + "fnet", model.Catchpoint);
                return Utils.ExecCmd(cmd);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // PUT: fnet/start
        [HttpPut("start")]
        public ActionResult<string> StartFnetService()
        {
            try
            {
                return Utils.ExecCmd(@"sc start ""Fnet Node""");
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // PUT: fnet/stop
        [HttpPut("stop")]
        public ActionResult<string> StopFnetService()
        {
            try
            {
                return Utils.ExecCmd(@"sc stop ""Fnet Node""");
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // DELETE: fnet
        [HttpDelete]
        public ActionResult<string> DeleteFnetService()
        {
            try
            {
                return Utils.ExecCmd(@"sc delete ""Fnet Node""");
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}
