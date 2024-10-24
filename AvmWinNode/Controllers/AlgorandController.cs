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
        public async Task<ActionResult<NodeStatus>> GetAlgorand()
        {
            try
            {
                string net = ":0";
                try { net = System.IO.File.ReadAllText(_dataPath + @"algorand\algod.net").Replace("\n", ""); } catch { }
                int port = int.Parse(net[(net.LastIndexOf(":") + 1)..]);
                string token = string.Empty;
                try { token = System.IO.File.ReadAllText(_dataPath + @"algorand\algod.admin.token"); } catch { }
                string sc = await Utils.ExecCmd(@"sc query ""Algorand Node""");
                string serviceStatus = Utils.ParseServiceStatus(sc);
                NodeStatus nodeStatus = new()
                {
                    Port = port,
                    Token = token,
                    ServiceStatus = serviceStatus
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
                var round = model.Catchpoint.Split('#')[0];
                var data = model.Catchpoint.Split('#')[1];
                if (string.IsNullOrEmpty(model.Catchpoint)
                    || model.Catchpoint.Any(Char.IsWhiteSpace)
                    || !int.TryParse(round, out _)
                    || data.Length != 52)
                    return BadRequest();
                string cmd = string.Format(_dataPath + "goal node catchup {0} -d " + _dataPath + "algorand", model.Catchpoint);
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
    }
}
