using AvmWinNode.Models;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
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
        public async Task<ActionResult<NodeStatus>> GetAlgorand()
        {
            try
            {
                int port = 0;
                string token = string.Empty;
                try { token = System.IO.File.ReadAllText(_dataPath + @"algorand\algod.admin.token"); } catch { }
                bool p2p = false;
                string? configText = null;
                try { configText = System.IO.File.ReadAllText(_dataPath + @"algorand\config.json"); } catch { }
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
                string sc = await Utils.ExecCmd(@"sc query ""Algorand Node""");
                string serviceStatus = Utils.ParseServiceStatus(sc);
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

        // POST: algorand
        [HttpPost]
        public async Task<ActionResult<string>> CreateAlgorandService()
        {
            try
            {
                if (!Directory.Exists(_dataPath + "algorand"))
                {
                    await Utils.ExecCmd(@"tar -xf """ + AppContext.BaseDirectory + @"Templates\algorand.zip"" -C " + _dataPath);
                }
                string binPath = @"""\""" + AppContext.BaseDirectory + @"Services\NodeService.exe\"" algorand""";
                return await Utils.ExecCmd(@"sc create ""Algorand Node"" binPath= " + binPath + @" start= auto");
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // POST: algorand/reset
        [HttpPost("reset")]
        public async Task<ActionResult<string>> ResetAlgorandNode()
        {
            try
            {
                if (Directory.Exists(_dataPath + "algorand"))
                {
                    Directory.Delete(_dataPath + "algorand", true);
                }
                await Utils.ExecCmd(@"tar -xf """ + AppContext.BaseDirectory + @"Templates\algorand.zip"" -C " + _dataPath);
                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // POST: algorand/catchup
        [HttpPost("catchup")]
        public async Task<ActionResult<string>> CatchupAlgorandNode(Catchup model)
        {
            try
            {
                string cmd = $"{_dataPath}goal node catchup {model.Round}#{model.Label} -d {_dataPath}algorand";
                return await Utils.ExecCmd(cmd);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // PUT: algorand/start
        [HttpPut("start")]
        public async Task<ActionResult<string>> StartAlgorandService()
        {
            try
            {
                return await Utils.ExecCmd(@"sc start ""Algorand Node""");
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // PUT: algorand/stop
        [HttpPut("stop")]
        public async Task<ActionResult<string>> StopAlgorandService()
        {
            try
            {
                return await Utils.ExecCmd(@"sc stop ""Algorand Node""");
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // DELETE: algorand
        [HttpDelete]
        public async Task<ActionResult<string>> DeleteAlgorandService()
        {
            try
            {
                return await Utils.ExecCmd(@"sc delete ""Algorand Node""");
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // GET: algorand/config
        [HttpGet("config")]
        public async Task<ActionResult<string>> GetConfig()
        {
            if (!Directory.Exists($"{_dataPath}algorand"))
            {
                await Utils.ExecCmd($@"tar -xf ""{AppContext.BaseDirectory}Templates\algorand.zip"" -C {_dataPath}");
            }
            string config = System.IO.File.ReadAllText($@"{_dataPath}algorand\config.json");
            return config;
        }

        // PUT: algorand/config
        [HttpPut("config")]
        public ActionResult SetConfig(Config model)
        {
            using (StreamWriter writer = new($@"{_dataPath}algorand\config.json", false))
            {
                writer.Write(model.Json);
            }
            return Ok();
        }
    }
}
