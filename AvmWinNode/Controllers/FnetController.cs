using AvmWinNode.Models;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
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
        public async Task<ActionResult<NodeStatus>> GetFnet()
        {
            try
            {
                int port = 0;
                string token = string.Empty;
                try { token = System.IO.File.ReadAllText(_dataPath + @"fnet\algod.admin.token"); } catch { }
                string fnetQuery = await Utils.ExecCmd(@"sc query ""Fnet Node""");
                string nodeServiceStatus = Utils.ParseServiceStatus(fnetQuery);
                bool p2p = false;
                string? configText = null;
                try { configText = System.IO.File.ReadAllText(_dataPath + @"fnet\config.json"); } catch { }
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
                // Reti Status
                string retiQuery = await Utils.ExecCmd(@"sc query ""Reti Validator""");
                string retiServiceStatus = Utils.ParseServiceStatus(retiQuery);

                string exePath = _dataPath + @"reti\reti.exe";
                string? version = null;
                if (System.IO.File.Exists(exePath))
                {
                    version = await Utils.ExecCmd(exePath + " --version");
                }

                string? exeStatus = null;
                if (retiServiceStatus == "Running")
                {
                    try
                    {
                        using HttpClient client = new();
                        var ready = await client.GetAsync("http://localhost:6260/ready");
                        exeStatus = ready.IsSuccessStatusCode ? "Running" : "Stopped";
                    }
                    catch
                    {
                        exeStatus = "Stopped";
                    }
                }

                RetiStatus retiStatus = new()
                {
                    ServiceStatus = retiServiceStatus,
                    Version = version,
                    ExeStatus = exeStatus,
                };

                NodeStatus nodeStatus = new()
                {
                    ServiceStatus = nodeServiceStatus,
                    Port = port,
                    Token = token,
                    P2p = p2p,
                    RetiStatus = retiStatus,
                };

                return nodeStatus;
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // POST: fnet
        [HttpPost]
        public async Task<ActionResult<string>> CreateFnetService()
        {
            try
            {
                if (!Directory.Exists(_dataPath + "fnet"))
                {
                    await Utils.ExecCmd(@"tar -xf """ + AppContext.BaseDirectory + @"Templates\fnet.zip"" -C " + _dataPath);
                }
                string binPath = @"""\""" + AppContext.BaseDirectory + @"Services\NodeService.exe\"" fnet""";
                return await Utils.ExecCmd(@"sc create ""Fnet Node"" binPath= " + binPath + @" start= auto");
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // POST: fnet/reset
        [HttpPost("reset")]
        public async Task<ActionResult<string>> ResetFnetNode()
        {
            try
            {
                if (Directory.Exists(_dataPath + "fnet"))
                {
                    Directory.Delete(_dataPath + "fnet", true);
                }
                await Utils.ExecCmd(@"tar -xf """ + AppContext.BaseDirectory + @"Templates\fnet.zip"" -C " + _dataPath);
                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // POST: fnet/catchup
        [HttpPost("catchup")]
        public async Task<ActionResult<string>> CatchupFnetNode(Catchup model)
        {
            try
            {
                string cmd = $"{_dataPath}goal node catchup {model.Round}#{model.Label} -d {_dataPath}fnet";
                return await Utils.ExecCmd(cmd);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // PUT: fnet/start
        [HttpPut("start")]
        public async Task<ActionResult<string>> StartFnetService()
        {
            try
            {
                return await Utils.ExecCmd(@"sc start ""Fnet Node""");
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // PUT: fnet/stop
        [HttpPut("stop")]
        public async Task<ActionResult<string>> StopFnetService()
        {
            try
            {
                return await Utils.ExecCmd(@"sc stop ""Fnet Node""");
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // DELETE: fnet
        [HttpDelete]
        public async Task<ActionResult<string>> DeleteFnetService()
        {
            try
            {
                return await Utils.ExecCmd(@"sc delete ""Fnet Node""");
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // GET: fnet/config
        [HttpGet("config")]
        public ActionResult<string> GetConfig()
        {
            string config = string.Empty;
            try { config = System.IO.File.ReadAllText($@"{_dataPath}fnet\config.json"); } catch { }
            return config;
        }

        // PUT: fnet/config
        [HttpPut("config")]
        public ActionResult SetConfig(Config model)
        {
            using (StreamWriter writer = new($@"{_dataPath}fnet\config.json", false))
            {
                writer.Write(model.Json);
            }
            return Ok();
        }
    }
}
