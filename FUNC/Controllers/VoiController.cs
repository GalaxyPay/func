using FUNC.Models;
using Microsoft.AspNetCore.Mvc;

namespace FUNC.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class VoiController(ILogger<VoiController> logger) : ControllerBase
    {
        private readonly ILogger<VoiController> _logger = logger;
        private static readonly string _name = "voi";

        // GET: voi
        [HttpGet]
        public async Task<ActionResult<NodeStatus>> GetNode()
        {
            try
            {
                return await Node.Get(_name);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // POST: voi
        [HttpPost]
        public async Task<ActionResult<string>> CreateNodeService()
        {
            try
            {
                await Node.CreateService(_name);
                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // POST: voi/reset
        [HttpPost("reset")]
        public async Task<ActionResult> ResetNodeData()
        {
            try
            {
                await Node.ResetData(_name);
                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // POST: voi/catchup
        [HttpPost("catchup")]
        public async Task<ActionResult<string>> CatchupNode(Catchup model)
        {
            try
            {
                return await Node.Catchup(_name, model);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // PUT: voi/start
        [HttpPut("start")]
        public async Task<ActionResult> StartNodeService()
        {
            try
            {
                await Node.ControlService(_name, "start");
                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // PUT: voi/stop
        [HttpPut("stop")]
        public async Task<ActionResult> StopNodeService()
        {
            try
            {
                await Node.ControlService(_name, "stop");
                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // DELETE: voi
        [HttpDelete]
        public async Task<ActionResult> DeleteNodeService()
        {
            try
            {
                await Node.ControlService(_name, "delete");
                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // GET: voi/config
        [HttpGet("config")]
        public async Task<ActionResult<string>> GetNodeConfig()
        {
            try
            {
                return await Node.GetConfig(_name);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // PUT: voi/config
        [HttpPut("config")]
        public ActionResult SetNodeConfig(Config model)
        {
            try
            {
                Node.SetConfig(_name, model);
                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // GET: voi/dir
        [HttpGet("dir")]
        public ActionResult<string> GetNodeDataDir()
        {
            try
            {
                return Utils.NodeDataParent(_name);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // PUT: voi/dir
        [HttpPut("dir")]
        public ActionResult<string> SetNodeDataDir(Dir model)
        {
            try
            {
                Node.SetDir(_name, model);
                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}
