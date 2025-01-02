﻿using System.Diagnostics;
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

        public static string ParseServiceStatus(string sc)
        {
            string status = "Unknown";
            if (IsWindows())
            {
                if (sc.Contains("1060:")) { status = "Not Found"; }
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
                else if (sc.StartsWith("-")) { status = "Stopped"; }
                else { status = "Running"; }
            }
            return status;
        }
    }
}
