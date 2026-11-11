using System.Diagnostics;
using System.Text.RegularExpressions;
using FUNC.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using static System.OperatingSystem;

namespace FUNC
{
    // Algorand nodes installed outside FUNC: the apt/rpm package (which NodeKit also uses
    // on Linux), NodeKit's Homebrew + launchd setup on macOS, or go-algorand's update.sh.
    // FUNC detects them so it never installs over one, and adopts the mainnet one as its
    // "algorand" node, managing it in place and keeping it usable by those tools.
    public static partial class External
    {
        private static readonly string statePath = Path.Combine(Utils.appDataDir, "algorand.json");
        private static readonly string funcAlgod = Path.Combine(Utils.appDataDir, "bin", "algod");
        private const string MacLabel = "com.algorand.algod";
        private const string LinuxDefaultDataDir = "/var/lib/algorand";

        // The adopted node, or null when FUNC manages its own algorand node.
        public static ExternalNode? Load()
        {
            if (IsWindows()) return null;
            try { return JsonConvert.DeserializeObject<ExternalNode>(File.ReadAllText(statePath)); } catch { return null; }
        }

        private static void Save(ExternalNode node) => File.WriteAllText(statePath, JsonConvert.SerializeObject(node, Formatting.Indented));

        private static void Release() => File.Delete(statePath);

        // Adopt an existing mainnet node while FUNC has no algorand node of its own, and
        // forget an adopted node once it has been removed outside FUNC (apt remove,
        // nodekit uninstall, ...). The record is kept because a moved node may no longer
        // be at a path detection looks at.
        public static async Task Sync(Func<Task<bool>> funcNodeExists)
        {
            if (IsWindows()) return;
            if (Load() is { } adopted)
            {
                if (await Status(adopted) == "Not Found") Release();
                return;
            }
            if (await funcNodeExists()) return;
            if (await Detect() is { Network: "mainnet" } found) Save(found);
        }

        // Whether a systemd unit runs FUNC's own algod (FUNC units of every version do).
        public static bool IsFuncUnit(string unit)
        {
            try { return File.ReadAllText($"/lib/systemd/system/{unit}.service").Contains(funcAlgod); } catch { return false; }
        }

        #region Detection

        public static async Task<ExternalNode?> Detect()
        {
            if (IsWindows()) return null;
            ExternalNode? node = null;
            try
            {
                node = IsLinux() ? await DetectLinux() : IsMacOS() ? await DetectMac() : null;
                if (node != null) node.Network = ReadNetwork(node.DataDir);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"External node detection failed: {ex.Message}");
            }
            return node;
        }

        private static async Task<ExternalNode?> DetectLinux()
        {
            // The apt/rpm package, as installed manually or by NodeKit. NodeKit may point it
            // at another data dir with a drop-in, which `systemctl show` already reflects.
            string? manager = null;
            if ((await Try("dpkg-query", ["-W", "-f=${Status}", "algorand"])).Stdout.Contains("install ok installed")) manager = "apt";
            else if ((await Try("rpm", ["-q", "algorand"])).ExitCode == 0) manager = "rpm";
            if (manager != null && !IsFuncUnit("algorand"))
            {
                var unit = await SystemdUnit("algorand");
                if (unit != null)
                    return new ExternalNode { Manager = manager, Service = "algorand", BinDir = Path.GetDirectoryName(unit.Value.Algod)!, DataDir = unit.Value.DataDir ?? LinuxDefaultDataDir, User = unit.Value.User };
            }

            // update.sh installs, optionally run by go-algorand's algorand@.service template.
            string units = (await Try("systemctl", ["list-units", "--all", "--plain", "--no-legend", "--type=service", "algorand@*"])).Stdout;
            foreach (string line in units.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                string name = line.Split(' ')[0];
                var unit = await SystemdUnit(name);
                if (unit?.DataDir == null) continue;
                string binDir = Path.GetDirectoryName(unit.Value.Algod)!;
                return new ExternalNode { Manager = UpdaterManager(binDir), Service = name, BinDir = binDir, DataDir = unit.Value.DataDir, User = unit.Value.User };
            }

            return await DetectUnmanaged(["/root/node/data", .. HomeDirs("/home").Select(h => Path.Combine(h, "node", "data"))]);
        }

        private static async Task<ExternalNode?> DetectMac()
        {
            // NodeKit's launchd daemon (algod from Homebrew), or one set up by hand.
            string plist = $"/Library/LaunchDaemons/{MacLabel}.plist";
            if (File.Exists(plist))
            {
                var args = await PlistArgs(plist);
                int d = Array.IndexOf(args, "-d");
                // `nodekit uninstall` without --force removes algod but leaves the plist.
                if (args.Length > 0 && d >= 0 && d + 1 < args.Length && File.Exists(args[0]))
                {
                    string algod = args[0];
                    string binDir = Path.GetDirectoryName(algod)!;
                    var user = await Try("plutil", ["-extract", "UserName", "raw", "-o", "-", plist]);
                    return new ExternalNode
                    {
                        Manager = RealPath(algod).Contains("/Cellar/") ? "brew" : UpdaterManager(binDir),
                        Service = MacLabel,
                        BinDir = binDir,
                        DataDir = args[d + 1],
                        User = user.ExitCode == 0 ? user.Stdout.Trim() : null,
                    };
                }
            }
            string[] homes = HomeDirs("/Users");
            return await DetectUnmanaged([.. homes.Select(h => Path.Combine(h, "node", "data")), .. homes.Select(h => Path.Combine(h, ".algorand"))]);
        }

        // An algod started with goal (no service): a running process outside FUNC, or a
        // data dir at a well-known path with the binaries next to it, as update.sh lays out.
        private static async Task<ExternalNode?> DetectUnmanaged(string[] knownDataDirs)
        {
            string ps = (await Try("ps", ["ax", "-o", "args="])).Stdout;
            foreach (string line in ps.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                string[] args = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (Path.GetFileName(args[0]) != "algod" || !Path.IsPathRooted(args[0]) || args[0].StartsWith(Utils.appDataDir)) continue;
                int d = Array.IndexOf(args, "-d");
                if (d < 0 || d + 1 >= args.Length || !IsDataDir(args[d + 1])) continue;
                string binDir = Path.GetDirectoryName(args[0])!;
                return new ExternalNode { Manager = UpdaterManager(binDir), BinDir = binDir, DataDir = args[d + 1], User = await Owner(args[d + 1]) };
            }
            foreach (string dataDir in knownDataDirs.Where(IsDataDir))
            {
                string binDir = Path.GetDirectoryName(dataDir)!;
                if (!File.Exists(Path.Combine(binDir, "algod"))) continue;
                return new ExternalNode { Manager = UpdaterManager(binDir), BinDir = binDir, DataDir = dataDir, User = await Owner(dataDir) };
            }
            return null;
        }

        private static bool IsDataDir(string dir) => File.Exists(Path.Combine(dir, "genesis.json"));

        private static string UpdaterManager(string binDir) => File.Exists(Path.Combine(binDir, "update.sh")) ? "updater" : "manual";

        private static string[] HomeDirs(string root)
        {
            try { return Directory.GetDirectories(root); } catch { return []; }
        }

        private static string? ReadNetwork(string dataDir)
        {
            try { return JObject.Parse(File.ReadAllText(Path.Combine(dataDir, "genesis.json")))["network"]?.Value<string>(); } catch { return null; }
        }

        // `systemctl show` prints ExecStart as "{ path=... ; argv[]=/usr/bin/algod -d /var/lib/algorand ; ... }".
        [GeneratedRegex(@"argv\[\]=(\S+)([^;]*)")]
        private static partial Regex ExecStartArgv();

        [GeneratedRegex(@"(?:^|\s)-d\s+(\S+)")]
        private static partial Regex DataDirArg();

        [GeneratedRegex(@"ALGORAND_DATA=(\S+)")]
        private static partial Regex AlgorandDataEnv();

        private static async Task<(string Algod, string? DataDir, string? User)?> SystemdUnit(string unit)
        {
            return ParseSystemdShow((await Try("systemctl", ["show", unit, "-p", "LoadState", "-p", "ExecStart", "-p", "User", "-p", "Environment"])).Stdout);
        }

        // The algod path, data dir and account from `systemctl show` output, or null if
        // the unit isn't loaded or doesn't run a command.
        internal static (string Algod, string? DataDir, string? User)? ParseSystemdShow(string show)
        {
            var props = show.Split('\n').Select(l => l.Split('=', 2)).Where(p => p.Length == 2).ToDictionary(p => p[0], p => p[1].Trim());
            if (props.GetValueOrDefault("LoadState") != "loaded") return null;
            var argv = ExecStartArgv().Match(props.GetValueOrDefault("ExecStart") ?? "");
            if (!argv.Success) return null;
            var d = DataDirArg().Match(argv.Groups[2].Value);
            var env = AlgorandDataEnv().Match(props.GetValueOrDefault("Environment") ?? "");
            string? dataDir = d.Success ? d.Groups[1].Value : env.Success ? env.Groups[1].Value : null;
            string? user = props.GetValueOrDefault("User");
            return (argv.Groups[1].Value, dataDir, string.IsNullOrEmpty(user) ? null : user);
        }

        private static async Task<string[]> PlistArgs(string plist)
        {
            var json = await Try("plutil", ["-extract", "ProgramArguments", "json", "-o", "-", plist]);
            try { return JArray.Parse(json.Stdout).Select(t => t.Value<string>() ?? "").ToArray(); } catch { return []; }
        }

        #endregion

        #region Management

        public static async Task<string> Status(ExternalNode node)
        {
            if (node.Service == null)
                return !Directory.Exists(node.DataDir) ? "Not Found" : IsAlgodRunning(node) ? "Running" : "Stopped";
            string status = await Utils.ServiceStatus(node.Service, string.Empty, node.Service);
            // Removed outside FUNC but its service left behind: `apt remove` masks the unit
            // (which reads as stopped) and `nodekit uninstall` without --force keeps the plist.
            // brew briefly unlinks algod while upgrading, so don't count that as removed.
            if (status != "Running" && !updating && !File.Exists(Path.Combine(node.BinDir, "algod"))) return "Not Found";
            return status;
        }

        private static volatile bool updating;

        public static async Task Control(ExternalNode node, string cmd)
        {
            if (cmd == "delete")
            {
                throw new Exception("The existing node's service can't be removed; delete the node data instead");
            }
            else if (node.Service == null)
            {
                await RunAs(node.User, Path.Combine(node.BinDir, "goal"), "node", cmd, "-d", node.DataDir);
            }
            else if (IsLinux())
            {
                await Utils.Systemctl(cmd, node.Service);
            }
            else if (IsMacOS())
            {
                // NodeKit's plist has no KeepAlive and NodeKit drives it with launchctl
                // start/stop, so keep the job loaded rather than booting it out.
                bool loaded = (await Try("launchctl", ["list", node.Service])).ExitCode == 0;
                if (!loaded && cmd != "stop") await Utils.Launchctl("bootstrap", "system", $"/Library/LaunchDaemons/{node.Service}.plist");
                else if (cmd == "start") await Utils.Launchctl("kickstart", $"system/{node.Service}");
                else if (cmd == "restart") await Utils.Launchctl("kickstart", "-k", $"system/{node.Service}");
                else if (cmd == "stop") await Utils.Launchctl("stop", node.Service);
            }
        }

        // Update the node's binaries with whatever installed them, then restart it.
        public static async Task Update(ExternalNode node)
        {
            bool running = await Status(node) == "Running";
            Utils.ProcResult result;
            updating = true;
            try
            {
                switch (node.Manager)
                {
                    case "apt":
                        await Try("apt-get", ["update"], Noninteractive);
                        result = await Try("apt-get", ["install", "--only-upgrade", "-y", "algorand"], Noninteractive);
                        break;
                    case "rpm":
                        result = await Try("dnf", ["update", "-y", "--refresh", "algorand"]);
                        break;
                    case "brew":
                        var (brew, owner) = await Brew(node);
                        result = await RunAs(owner, brew, "upgrade", "algorand", "--formula");
                        break;
                    case "updater":
                        // -n: FUNC restarts the node itself, through its service if it has one.
                        result = await RunAs(node.User, Path.Combine(node.BinDir, "update.sh"), "-n", "-verify", "-p", node.BinDir, "-d", node.DataDir);
                        break;
                    default:
                        throw new Exception("FUNC doesn't know how this node was installed, so it can't update it");
                }
            }
            finally { updating = false; }
            if (result.ExitCode != 0) throw new Exception($"Node update failed: {Last(result.Stderr + result.Stdout)}");
            if (running) await Control(node, "restart");
        }

        // Move the data dir to <parent>/algorand and leave a symlink at the configured path,
        // so everything already pointing there keeps working unchanged: the service's -d,
        // NodeKit's default path, and any -d or ALGORAND_DATA the user set for goal/NodeKit.
        public static async Task Move(ExternalNode node, string parent)
        {
            string target = Path.Combine(parent, "algorand");
            string current = RealDataDir(node);
            bool isLink = current != node.DataDir;
            if (target == current) throw new Exception("Data directory is already at that location");
            if (target.StartsWith(current + "/")) throw new Exception("New location must not be inside the current data directory");
            if (target.StartsWith(Utils.appDataDir + "/")) throw new Exception("New location must not be inside FUNC's own directory");
            if (Directory.Exists(target) && Directory.EnumerateFileSystemEntries(target).Any()) throw new Exception("New location must be empty");

            bool running = await Status(node) == "Running";
            if (running) await Control(node, "stop");
            await WaitStopped(node);

            if (Directory.Exists(target)) Directory.Delete(target);
            Directory.CreateDirectory(parent);
            if (isLink) File.Delete(node.DataDir);
            // cp -a keeps the files' owner, so the node's account can still use them.
            var copy = await Try("cp", ["-a", current, target]);
            if (copy.ExitCode != 0)
            {
                if (isLink) Directory.CreateSymbolicLink(node.DataDir, current);
                throw new Exception($"Copying the data directory failed: {Last(copy.Stderr)}");
            }
            Directory.Delete(current, true);

            // Moving back to the configured path leaves no link.
            if (target != node.DataDir) Directory.CreateSymbolicLink(node.DataDir, target);

            if (running) await Control(node, "start");
        }

        // Remove the node and how it was installed, including its participation keys, so
        // FUNC can install its own node in its place.
        public static async Task Wipe(ExternalNode node)
        {
            string current = RealDataDir(node);
            if (await Status(node) == "Running") await Control(node, "stop");
            await WaitStopped(node);

            Utils.ProcResult result = new(0, "", "");
            if (node.Manager is "apt" or "rpm")
            {
                // purge, not remove: the deb's postrm masks algorand.service on remove (only
                // purge unmasks it), which would block FUNC's own unit of the same name.
                result = node.Manager == "apt"
                    ? await Try("apt-get", ["purge", "-y", "algorand"], Noninteractive)
                    : await Try("dnf", ["remove", "-y", "algorand"]);
            }
            else if (node.Service != null && IsLinux())
            {
                await Utils.Systemctl("disable", "--now", node.Service);
            }
            if (result.ExitCode != 0) throw new Exception($"Removing the algorand package failed: {Last(result.Stderr + result.Stdout)}");

            if (IsLinux() && node.Service != null)
            {
                // NodeKit's (or FUNC's) drop-in would otherwise linger and repoint a future unit.
                string unit = node.Service.EndsWith(".service") ? node.Service : $"{node.Service}.service";
                if (Directory.Exists($"/etc/systemd/system/{unit}.d")) Directory.Delete($"/etc/systemd/system/{unit}.d", true);
                await Utils.Systemctl("daemon-reload");
            }
            else if (IsMacOS() && node.Service != null)
            {
                string plist = $"/Library/LaunchDaemons/{node.Service}.plist";
                await Utils.Launchctl("bootout", "system", plist);
                File.Delete(plist);
                // Brew() can't locate the install once algod is gone (already uninstalled).
                if (node.Manager == "brew" && File.Exists(Path.Combine(node.BinDir, "algod")))
                {
                    var (brew, owner) = await Brew(node);
                    result = await RunAs(owner, brew, "uninstall", "algorand");
                    if (result.ExitCode != 0) Console.WriteLine($"brew uninstall algorand failed: {Last(result.Stderr)}");
                }
            }

            if (current != node.DataDir) File.Delete(node.DataDir);
            if (Directory.Exists(current)) Directory.Delete(current, true);
            Release();
        }

        #endregion

        #region Helpers

        // Run a command as the node's account so anything it writes keeps the right owner.
        public static Task<Utils.ProcResult> RunAs(string? user, string file, params string[] args)
        {
            if (string.IsNullOrEmpty(user) || user == "root") return Try(file, args);
            return IsLinux()
                ? Try("runuser", ["-u", user, "--", file, .. args])
                : Try("sudo", ["-u", user, "-H", file, .. args]);
        }

        private static readonly Dictionary<string, string> Noninteractive = new() { ["DEBIAN_FRONTEND"] = "noninteractive" };

        // Like Utils.RunProcess, but a missing executable is a failed result, not an exception.
        private static async Task<Utils.ProcResult> Try(string file, IEnumerable<string> args, IDictionary<string, string>? env = null)
        {
            try { return await Utils.RunProcess(file, args, env); }
            catch (Exception ex) { return new Utils.ProcResult(-1, string.Empty, ex.Message); }
        }

        private static string Last(string output) => output.Trim().Split('\n').LastOrDefault() ?? string.Empty;

        private static async Task<string?> Owner(string path)
        {
            var stat = await Try("stat", IsLinux() ? ["-c", "%U", path] : ["-f", "%Su", path]);
            return stat.ExitCode == 0 ? stat.Stdout.Trim() : null;
        }

        private static string RealPath(string path)
        {
            try { return File.ResolveLinkTarget(path, true)?.FullName ?? path; } catch { return path; }
        }

        public static string RealDataDir(ExternalNode node)
        {
            try { return Directory.ResolveLinkTarget(node.DataDir, true)?.FullName ?? node.DataDir; } catch { return node.DataDir; }
        }

        // Homebrew refuses to run as root; run it as the account that owns the install.
        private static async Task<(string Brew, string? Owner)> Brew(ExternalNode node)
        {
            string cellarAlgod = RealPath(Path.Combine(node.BinDir, "algod"));
            int cellar = cellarAlgod.IndexOf("/Cellar/");
            if (cellar < 0) throw new Exception("algod is not installed by Homebrew");
            return (Path.Combine(cellarAlgod[..cellar], "bin", "brew"), await Owner(cellarAlgod));
        }

        private static bool IsAlgodRunning(ExternalNode node)
        {
            try
            {
                int pid = int.Parse(File.ReadAllText(Path.Combine(node.DataDir, "algod.pid")).Trim());
                using var process = Process.GetProcessById(pid);
                return !process.HasExited;
            }
            catch { return false; }
        }

        private static async Task WaitStopped(ExternalNode node)
        {
            for (int i = 0; i < 60 && IsAlgodRunning(node); i++) await Task.Delay(500);
            if (IsAlgodRunning(node)) throw new Exception("The node did not stop");
        }

        #endregion
    }
}
