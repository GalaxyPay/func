using AvmWinNode.Models;
using Microsoft.AspNetCore.Mvc;
using System;
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

        // GET: reti
        [HttpGet]
        public async Task<ActionResult<RetiStatus>> RetiStatus()
        {
            try
            {
                string exePath = _dataPath + @"reti\reti.exe";
                string? version = null;
                if (System.IO.File.Exists(exePath))
                {
                    version = Utils.ExecCmd(exePath + " --version");
                }

                string sc = Utils.ExecCmd(@"sc query ""Reti Validator""");
                string serviceStatus = Utils.ParseServiceStatus(sc);
                string exeStatus;
                try
                {
                    using HttpClient client = new();
                    var test = await client.GetAsync("http://localhost:6260/ready");
                    exeStatus = test.IsSuccessStatusCode ? "Running" : "Stopped";
                }
                catch
                {
                    exeStatus = "Stopped";
                }

                if(serviceStatus=="Running" && exeStatus=="Stopped")
                {
                    StopRetiService();
                    StartRetiService();
                }

                RetiStatus response = new()
                {
                    Version = version,
                    ServiceStatus = serviceStatus,
                    ExeStatus = exeStatus,
                };
                return response;
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // GET: reti/version
        [HttpGet("version")]
        public ActionResult<string> RetiVersion(string latest)
        {
            try
            {
                string exePath = _dataPath + @"reti\reti.exe";
                if (!System.IO.File.Exists(exePath))
                {
                    string zipPath = _releasePath + "reti-" + latest + "-windows-amd64.zip";
                    Utils.ExecCmd("curl -sL -o " + _dataPath + "reti.zip " + zipPath);
                    var test = Utils.ExecCmd(@"tar -xf " + _dataPath + "reti.zip -C " + _dataPath + "reti");
                }
                string output = Utils.ExecCmd(exePath + " --version");
                return output;
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // POST: reti
        [HttpPost]
        public ActionResult<string> CreateRetiService(RetiCreate model)
        {
            try
            {
                string exePath = _dataPath + @"reti\reti.exe";
                if (!System.IO.File.Exists(exePath))
                {
                    string zipPath = _releasePath + "reti-" + model.Latest + "-windows-amd64.zip";
                    Utils.ExecCmd("curl -sL -o " + _dataPath + "reti.zip " + zipPath);
                    var test = Utils.ExecCmd(@"tar -xf " + _dataPath + "reti.zip -C " + _dataPath + "reti");
                }

                string envPath = _dataPath + @"reti\.env";
                if (System.IO.File.Exists(envPath))
                {
                    System.IO.File.Delete(envPath);
                }
                using (StreamWriter sw = System.IO.File.CreateText(envPath))
                {
                    sw.WriteLine(model.Env);
                }
                return Utils.ExecCmd(@"sc create ""Reti Validator"" binPath= """ + AppContext.BaseDirectory + @"Services\RetiService.exe"" start= delayed-auto");
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // POST: reti/update
        [HttpPost("update")]
        public ActionResult UpdateReti(RetiUpdate model)
        {
            try
            {
                string exePath = _dataPath + @"reti\reti.exe";
                string zipPath = _releasePath + "reti-" + model.Latest + "-windows-amd64.zip";
                Utils.ExecCmd("curl -sL -o " + _dataPath + "reti.zip " + zipPath);
                var test = Utils.ExecCmd(@"tar -xf " + _dataPath + "reti.zip -C " + _dataPath + "reti");
                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // PUT: reti/start
        [HttpPut("start")]
        public ActionResult<string> StartRetiService()
        {
            try
            {
                return Utils.ExecCmd(@"sc start ""Reti Validator""");
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // PUT: reti/stop
        [HttpPut("stop")]
        public ActionResult<string> StopRetiService()
        {
            try
            {
                return Utils.ExecCmd(@"sc stop ""Reti Validator""");
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // DELETE: reti
        [HttpDelete]
        public ActionResult<string> DeleteRetiService()
        {
            try
            {
                return Utils.ExecCmd(@"sc delete ""Reti Validator""");
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}
