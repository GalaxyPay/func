using System.Diagnostics;
using static System.Environment;
using static System.OperatingSystem;

namespace FUNC
{
    public class Utils
    {
        public static readonly string dataPath = Path.Combine(GetFolderPath(SpecialFolder.CommonApplicationData), "func");

        public static string Cap(string name)
        {
            return Thread.CurrentThread.CurrentCulture.TextInfo.ToTitleCase(name);
        }

        public static async Task<string> ExecCmd(string cmd)
        {
            Console.WriteLine(cmd);
            Process p = new();
            if (IsWindows())
            {
                p.StartInfo.FileName = "cmd.exe";
                p.StartInfo.Arguments = "/C " + cmd;
            }
            else if (IsLinux())
            {
                p.StartInfo.FileName = "sh";
                p.StartInfo.Arguments = $"-c \"{cmd}\"";
            }
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
            if (IsWindows())
            {
                if (sc.Contains("OpenService FAILED")) { status = "Not Found"; }
                else if (sc.Contains("1  STOPPED")) { status = "Stopped"; }
                else if (sc.Contains("4  RUNNING")) { status = "Running"; }
            }
            else if (IsLinux())
            {
                if (sc.Contains("not-found")) { status = "Not Found"; }
                else if (sc.Contains("=inactive")) { status = "Stopped"; }
                else if (sc.Contains("=active")) { status = "Running"; }
            }
            return status;
        }
    }
}
