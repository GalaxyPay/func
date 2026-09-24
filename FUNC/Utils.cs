using System.Diagnostics;
using static System.Environment;
using static System.OperatingSystem;

namespace FUNC
{
    public class Utils
    {
        public static readonly string appDataDir = IsMacOS() ? "/usr/local/share/func" : Path.Combine(GetFolderPath(SpecialFolder.CommonApplicationData), "func");

        public static string NodeDataParent(string name)
        {
            string nodeDataParent = appDataDir;
            string path = Path.Combine(appDataDir, $"{name}.data");
            try { nodeDataParent = File.ReadAllText(path); } catch { }
            return nodeDataParent.Trim();
        }

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
            else if (IsLinux() || IsMacOS())
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

        public record ProcResult(int ExitCode, string Stdout, string Stderr);

        // Run an executable directly (no shell), with optional environment variables and
        // working directory, capturing stdout, stderr and the exit code. Unlike ExecCmd
        // this needs no shell quoting and surfaces the error output of tools like uv.
        public static async Task<ProcResult> RunProcess(string fileName, IEnumerable<string> args, IDictionary<string, string>? env = null, string? workingDir = null)
        {
            Console.WriteLine($"{fileName} {string.Join(' ', args)}");
            Process p = new();
            p.StartInfo.FileName = fileName;
            foreach (string a in args) p.StartInfo.ArgumentList.Add(a);
            if (workingDir != null) p.StartInfo.WorkingDirectory = workingDir;
            if (env != null) foreach (var (k, v) in env) p.StartInfo.Environment[k] = v;
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.RedirectStandardError = true;
            p.Start();
            // Drain both pipes concurrently so a chatty stream cannot deadlock the process.
            var stdout = p.StandardOutput.ReadToEndAsync();
            var stderr = p.StandardError.ReadToEndAsync();
            await Task.WhenAll(stdout, stderr);
            await p.WaitForExitAsync();
            return new ProcResult(p.ExitCode, stdout.Result, stderr.Result);
        }

        public static async Task RestartWindowsService(string svc)
        {
            await ExecCmd($"sc stop \"{svc}\"");
            // sc stop is async; wait for it to actually stop before starting.
            for (int i = 0; i < 20; i++)
            {
                string q = await ExecCmd($"sc query \"{svc}\"");
                if (q.Contains("STOPPED") || q.Contains("1060")) break;
                await Task.Delay(500);
            }
            await ExecCmd($"sc start \"{svc}\"");
        }

        public static string ParseServiceStatus(string sc)
        {
            string status = "Unknown";
            if (IsWindows())
            {
                if (sc.Contains("1060")) { status = "Not Found"; }
                else if (sc.Contains("1  STOPPED")) { status = "Stopped"; }
                else if (sc.Contains("4  RUNNING")) { status = "Running"; }
            }
            else if (IsLinux())
            {
                if (sc.Contains("not-found")) { status = "Not Found"; }
                else if (sc.Contains("=inactive")) { status = "Stopped"; }
                else if (sc.Contains("=active")) { status = "Running"; }
            }
            else if (IsMacOS())
            {
                if (sc.StartsWith("none")) { status = "Not Found"; }
                else if (sc.StartsWith("stopped")) { status = "Stopped"; }
                else if (sc.StartsWith("-")) { status = "Stopped"; }
                else { status = "Running"; }
            }
            return status;
        }
    }
}
