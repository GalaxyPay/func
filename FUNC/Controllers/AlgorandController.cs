using FUNC.Models;
using Microsoft.AspNetCore.Mvc;

namespace FUNC.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class AlgorandController(ILogger<AlgorandController> logger) : ControllerBase
    {
        private readonly ILogger<AlgorandController> _logger = logger;
        private static readonly string _name = "algorand";

        // GET: algorand
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

        // POST: algorand
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

        // POST: algorand/reset
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

        // POST: algorand/catchup
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

        // PUT: algorand/start
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

        // PUT: algorand/stop
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

        // DELETE: algorand
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

        // GET: algorand/config
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

        // PUT: algorand/config
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

        // GET: algorand/dir
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

        // PUT: algorand/dir
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
