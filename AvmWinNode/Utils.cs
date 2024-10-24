using System.Diagnostics;

namespace AvmWinNode
{
    public class Utils
    {
        public static async Task<string> ExecCmd(string cmd)
        {
            Process p = new();
            p.StartInfo.FileName = "cmd.exe";
            p.StartInfo.Arguments = "/C " + cmd;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.UseShellExecute = false;
            p.Start();
            var output = await p.StandardOutput.ReadToEndAsync();
            await p.WaitForExitAsync();
            return output;
        }

        public static string ParseServiceStatus(string sc)
        {
            string status = "Unknown";
            if (sc.Contains("OpenService FAILED")) { status = "Not Found"; }
            else if (sc.Contains("1  STOPPED")) { status = "Stopped"; }
            else if (sc.Contains("4  RUNNING")) { status = "Running"; }
            return status;
        }
    }
}
