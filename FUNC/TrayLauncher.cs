using System.Security;
using System.Text;
using Microsoft.Extensions.Hosting.WindowsServices;
using static System.OperatingSystem;

namespace FUNC
{
    // Windows: the service can't show a tray icon from Session 0, so it registers a
    // logon task that starts FUNC.Tray.exe for every signed-in user, and runs it now
    // so the icon is back right after an install or update (the installer stops it).
    // macOS uses a LaunchAgent set up by the pkg scripts instead.
    public static class TrayLauncher
    {
        public const string TaskName = "FUNC Tray";

        public static async Task EnsureWindowsTask()
        {
            if (!IsWindows() || !WindowsServiceHelpers.IsWindowsService()) return;
            string exe = Path.Combine(AppContext.BaseDirectory, "FUNC.Tray.exe");
            if (!File.Exists(exe)) return;

            // schtasks only reads task XML as UTF-16.
            string xmlPath = Path.Combine(Path.GetTempPath(), "func-tray-task.xml");
            File.WriteAllText(xmlPath, TaskXml(exe), Encoding.Unicode);
            try
            {
                string created = await Utils.Exec("schtasks.exe", "/Create", "/F", "/TN", TaskName, "/XML", xmlPath);
                if (!created.Contains("SUCCESS")) Console.WriteLine($"Tray task: {created.Trim()}");
                await Utils.Exec("schtasks.exe", "/Run", "/TN", TaskName);
            }
            finally
            {
                File.Delete(xmlPath);
            }
        }

        // Runs as each member of BUILTIN\Users at logon, unelevated, in their session.
        internal static string TaskXml(string exe) => $"""
            <?xml version="1.0" encoding="UTF-16"?>
            <Task version="1.2" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">
              <RegistrationInfo><Description>Shows the FUNC tray icon for each signed-in user.</Description></RegistrationInfo>
              <Triggers><LogonTrigger><Enabled>true</Enabled></LogonTrigger></Triggers>
              <Principals><Principal id="Users"><GroupId>S-1-5-32-545</GroupId><RunLevel>LeastPrivilege</RunLevel></Principal></Principals>
              <Settings>
                <MultipleInstancesPolicy>Parallel</MultipleInstancesPolicy>
                <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
                <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
                <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
                <Priority>5</Priority>
              </Settings>
              <Actions Context="Users"><Exec><Command>{SecurityElement.Escape(exe)}</Command></Exec></Actions>
            </Task>
            """;
    }
}
