using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using static System.OperatingSystem;

namespace FUNC.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class FuncController(ILogger<FuncController> logger) : ControllerBase
    {
        private readonly ILogger<FuncController> _logger = logger;

        const string scriptBase = "https://raw.githubusercontent.com/GalaxyPay/func/main";

        // POST: func/update
        [HttpPost("update")]
        public async Task<ActionResult> FuncUpdate()
        {
            try
            {
                if (IsWindows())
                {
                    // Run the install script in a detached process; children of a
                    // service survive the installer stopping the FUNC service.
                    ProcessStartInfo psi = new()
                    {
                        FileName = "powershell.exe",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                    };
                    psi.ArgumentList.Add("-NoProfile");
                    psi.ArgumentList.Add("-ExecutionPolicy");
                    psi.ArgumentList.Add("Bypass");
                    psi.ArgumentList.Add("-Command");
                    psi.ArgumentList.Add($"irm {scriptBase}/install.ps1 | iex");
                    Process.Start(psi);
                }
                else if (IsLinux())
                {
                    // systemd-run puts the upgrade in its own transient unit so it
                    // isn't killed when the package's prerm stops the func service
                    // (systemd kills the service's whole cgroup).
                    ProcessStartInfo psi = new()
                    {
                        FileName = "systemd-run",
                        UseShellExecute = false,
                    };
                    psi.ArgumentList.Add("--collect");
                    psi.ArgumentList.Add("/bin/sh");
                    psi.ArgumentList.Add("-c");
                    psi.ArgumentList.Add($"curl -fsSL {scriptBase}/install.sh | sh");
                    Process.Start(psi);
                }
                else if (IsMacOS())
                {
                    // Run the upgrade as its own launchd job so it isn't killed when
                    // the pkg's preinstall boots out func.api (launchd kills the
                    // job's process group).
                    string templatePath = Path.Combine(AppContext.BaseDirectory, "Templates", "func.update.plist");
                    System.IO.File.Copy(templatePath, "/Library/LaunchDaemons/func.update.plist", true);
                    await Utils.ExecCmd("launchctl bootout system/func.update"); // clear previous run, if any
                    await Utils.ExecCmd("launchctl bootstrap system /Library/LaunchDaemons/func.update.plist");
                }
                else return BadRequest();
                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}
