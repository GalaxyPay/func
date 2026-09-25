using System.Diagnostics;
using System.Text.RegularExpressions;
using static System.Environment;
using static System.OperatingSystem;

namespace FUNC
{
    public partial class Utils
    {
        public static readonly string appDataDir = IsMacOS() ? "/usr/local/share/func" : Path.Combine(GetFolderPath(SpecialFolder.CommonApplicationData), "func");

        public static string NodeDataParent(string name)
        {
            string nodeDataParent = appDataDir;
            string path = Path.Combine(appDataDir, $"{name}.data");
            try { nodeDataParent = File.ReadAllText(path); } catch { }
            return nodeDataParent.Trim();
        }

        // A caller-supplied node data parent directory. It ends up in process arguments,
        // a systemd unit and a launchd plist, so only accept an absolute, normalized path
        // made of ordinary path characters.
        [GeneratedRegex(@"^[\p{L}\p{N} _.:/\\-]+$")]
        private static partial Regex PathChars();

        public static string ValidateDataParent(string? path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new Exception("Path is required");
            path = path.Trim();
            if (!PathChars().IsMatch(path)) throw new Exception("Path contains unsupported characters");
            if (!Path.IsPathFullyQualified(path)) throw new Exception("Path must be absolute");
            string full = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (full != path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
                throw new Exception("Path must be normalized (no '..' or '.' segments)");
            if (full.Length == 0) throw new Exception("Path must not be the root directory");
            return full;
        }

        // A 25-word Algorand mnemonic. Whitespace is normalized to single spaces; anything
        // but lowercase words is rejected so the value can be written into a config file.
        public static string ValidateMnemonic(string? mnemonic)
        {
            if (string.IsNullOrWhiteSpace(mnemonic)) throw new Exception("Mnemonic is required");
            string[] words = mnemonic.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (words.Length != 25) throw new Exception("Mnemonic must be 25 words");
            foreach (string w in words)
            {
                if (w.Length > 16 || !w.All(c => c >= 'a' && c <= 'z'))
                    throw new Exception("Mnemonic must contain only lowercase words");
            }
            return string.Join(' ', words);
        }

        public static string Cap(string name)
        {
            return Thread.CurrentThread.CurrentCulture.TextInfo.ToTitleCase(name);
        }

        public record ProcResult(int ExitCode, string Stdout, string Stderr);

        // Run an executable directly (no shell), with optional environment variables and
        // working directory, capturing stdout, stderr and the exit code. Arguments are
        // passed as a list so no value is ever parsed by a shell.
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

        // Run an executable and return its stdout. A missing executable yields an empty
        // string rather than an exception, matching how a failed shell command behaved.
        public static async Task<string> Exec(string fileName, params string[] args)
        {
            try
            {
                return (await RunProcess(fileName, args)).Stdout;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{fileName} failed: {ex.Message}");
                return string.Empty;
            }
        }

        public static Task<string> Sc(params string[] args) => Exec("sc.exe", args);
        public static Task<string> Systemctl(params string[] args) => Exec("systemctl", args);
        public static Task<string> Launchctl(params string[] args) => Exec("launchctl", args);

        // Status of an OS service. `name` is the systemd unit / launchd label suffix
        // (e.g. "algorand", "reti"); `windowsName` is the Windows service name.
        public static async Task<string> ServiceStatus(string name, string windowsName)
        {
            if (IsWindows())
            {
                return ParseServiceStatus(await Sc("query", windowsName));
            }
            else if (IsLinux())
            {
                return ParseServiceStatus(await Systemctl("show", name, "--property=LoadState", "--property=ActiveState"));
            }
            else if (IsMacOS())
            {
                // `launchctl list` prints "PID Status Label" per loaded job; a PID of "-"
                // means loaded but not running. Not loaded but plist still on disk =
                // Stopped; no plist = Not Found.
                string label = $"func.{name}";
                string list = await Launchctl("list");
                string? line = list.Split('\n').FirstOrDefault(l => l.Contains(label, StringComparison.OrdinalIgnoreCase));
                if (line != null) return line.TrimStart().StartsWith('-') ? "Stopped" : "Running";
                return File.Exists($"/Library/LaunchDaemons/{label}.plist") ? "Stopped" : "Not Found";
            }
            return "Unknown";
        }

        // Ensure a dedicated service account exists. On Linux it is created on demand;
        // on macOS the pkg postinstall creates it. Returns whether the account exists.
        public static async Task<bool> EnsureUnixUser(string user)
        {
            var id = await RunProcess("id", ["-u", user]);
            if (id.ExitCode == 0) return true;
            if (!IsLinux()) return false;
            var add = await RunProcess("useradd", ["--system", "--create-home", "--home-dir", $"/var/lib/{user}", "--shell", "/usr/sbin/nologin", user]);
            return add.ExitCode == 0;
        }

        public static async Task ChownRecursive(string owner, string path)
        {
            await Exec("chown", "-R", owner, path);
        }

        public static async Task RestartWindowsService(string svc)
        {
            await Sc("stop", svc);
            // sc stop is async; wait for it to actually stop before starting.
            for (int i = 0; i < 20; i++)
            {
                string q = await Sc("query", svc);
                if (q.Contains("STOPPED") || q.Contains("1060")) break;
                await Task.Delay(500);
            }
            await Sc("start", svc);
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
            return status;
        }
    }
}
