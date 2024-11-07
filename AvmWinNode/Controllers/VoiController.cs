using AvmWinNode.Models;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
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
        public async Task<ActionResult<NodeStatus>> GetVoi()
        {
            try
            {
                int port = 0;
                string token = string.Empty;
                try { token = System.IO.File.ReadAllText(_dataPath + @"voi\algod.admin.token"); } catch { }
                string sc = await Utils.ExecCmd(@"sc query ""Voi Node""");
                string serviceStatus = Utils.ParseServiceStatus(sc);
                bool p2p = false;
                string? configText = null;
                try { configText = System.IO.File.ReadAllText(_dataPath + @"voi\config.json"); } catch { }
                if (configText != null)
                {
                    JObject config = JObject.Parse(configText);
                    var endpointAddressToken = config.GetValue("EndpointAddress");
                    string endpointAddress = endpointAddressToken?.Value<string>() ?? ":0";
                    port = int.Parse(endpointAddress[(endpointAddress.IndexOf(":") + 1)..]);
                    var enableP2PToken = config.GetValue("EnableP2P");
                    var enableP2PHybridModeToken = config.GetValue("EnableP2PHybridMode");
                    bool enableP2P = enableP2PToken != null && enableP2PToken.Value<bool>();
                    bool enableP2PHybridMode = enableP2PHybridModeToken != null && enableP2PHybridModeToken.Value<bool>();
                    if (enableP2P || enableP2PHybridMode) p2p = true;
                }
                NodeStatus nodeStatus = new()
                {
                    ServiceStatus = serviceStatus,
                    Port = port,
                    Token = token,
                    P2p = p2p,
                };
                return nodeStatus;
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // POST: voi
        [HttpPost]
        public async Task<ActionResult<string>> CreateVoiService()
        {
            try
            {
                if (!Directory.Exists(_dataPath + "voi"))
                {
                    await Utils.ExecCmd(@"tar -xf """ + AppContext.BaseDirectory + @"Templates\voi.zip"" -C " + _dataPath);
                }
                string binPath = @"""\""" + AppContext.BaseDirectory + @"Services\NodeService.exe\"" voi""";
                return await Utils.ExecCmd(@"sc create ""Voi Node"" binPath= " + binPath + @" start= auto");
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // POST: voi/reset
        [HttpPost("reset")]
        public async Task<ActionResult<string>> ResetVoiNode()
        {
            try
            {
                if (Directory.Exists(_dataPath + "voi"))
                {
                    Directory.Delete(_dataPath + "voi", true);
                }
                await Utils.ExecCmd(@"tar -xf """ + AppContext.BaseDirectory + @"Templates\voi.zip"" -C " + _dataPath);
                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // POST: voi/catchup
        [HttpPost("catchup")]
        public async Task<ActionResult<string>> CatchupVoiNode(Catchup model)
        {
            try
            {
                string cmd = $"{_dataPath}goal node catchup {model.Round}#{model.Label} -d {_dataPath}voi";
                return await Utils.ExecCmd(cmd);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // PUT: voi/start
        [HttpPut("start")]
        public async Task<ActionResult<string>> StartVoiService()
        {
            try
            {
                return await Utils.ExecCmd(@"sc start ""Voi Node""");
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // PUT: voi/stop
        [HttpPut("stop")]
        public async Task<ActionResult<string>> StopVoiService()
        {
            try
            {
                return await Utils.ExecCmd(@"sc stop ""Voi Node""");
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // DELETE: voi
        [HttpDelete]
        public async Task<ActionResult<string>> DeleteVoiService()
        {
            try
            {
                return await Utils.ExecCmd(@"sc delete ""Voi Node""");
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // GET: voi/config
        [HttpGet("config")]
        public ActionResult<string> GetConfig()
        {
            string config = string.Empty;
            try { config = System.IO.File.ReadAllText($@"{_dataPath}voi\config.json"); } catch { }
            return config;
        }

        // PUT: voi/config
        [HttpPut("config")]
        public ActionResult SetConfig(Config model)
        {
            using (StreamWriter writer = new($@"{_dataPath}voi\config.json", false))
            {
                writer.Write(model.Json);
            }
            return Ok();
        }
    }
}
