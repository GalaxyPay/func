using System.Formats.Tar;
using System.Text.RegularExpressions;
using FUNC.Controllers;
using FUNC.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using static System.OperatingSystem;

namespace FUNC
{
    public partial class Node
    {
        // Dedicated least-privilege account the node service runs as (non-root/non-SYSTEM).
        private const string LinuxNodeUser = "func-node";
        private const string MacNodeUser = "_func-node";

        private static string WinService(string name) => $"{Utils.Cap(name)} Node";
        private static string DataDir(string name) => Path.Combine(Utils.NodeDataParent(name), name);
        private static readonly string diagcfgPath = Path.Combine(Utils.appDataDir, "bin", "diagcfg");

        private static async Task ExtractTemplate(string name)
        {
            string templatePath = Path.Combine(AppContext.BaseDirectory, "Templates", $"{name}.tar");
            await TarFile.ExtractToDirectoryAsync(templatePath, Utils.NodeDataParent(name), true);
        }

        // FUNC runs elevated and creates the node data dir as root. The node service
        // itself runs as an unprivileged account, so hand ownership of the data dir to
        // that account. Call after any operation that creates or relocates the dir.
        private static async Task ApplyDirOwnership(string name)
        {
            string dataDir = DataDir(name);
            if (!Directory.Exists(dataDir)) return;
            if (IsLinux())
            {
                await Utils.EnsureUnixUser(LinuxNodeUser);
                await Utils.ChownRecursive($"{LinuxNodeUser}:{LinuxNodeUser}", dataDir);
            }
            else if (IsMacOS())
            {
                // The _func-node account is created by the pkg postinstall; skip if absent.
                if (await Utils.EnsureUnixUser(MacNodeUser))
                    await Utils.ChownRecursive(MacNodeUser, dataDir);
            }
            else if (IsWindows())
            {
                // Grant the per-service virtual account modify rights on its data dir.
                string account = $"NT SERVICE\\{WinService(name)}";
                await Utils.Exec("icacls.exe", dataDir, "/grant", $"{account}:(OI)(CI)M", "/T");
            }
        }

        // The algod admin token of a managed node, or empty if the node has no data dir yet.
        public static string AlgodToken(string name)
        {
            try { return File.ReadAllText(Path.Combine(DataDir(name), "algod.admin.token")).Trim(); } catch { return string.Empty; }
        }

        // The port algod listens on, from the node's config.json, or 0 if unknown.
        public static int AlgodPort(string name)
        {
            string? configText = null;
            try { configText = File.ReadAllText(Path.Combine(DataDir(name), "config.json")); } catch { }
            if (configText == null) return 0;
            JObject config = JObject.Parse(configText);
            string endpointAddress = config.GetValue("EndpointAddress")?.Value<string>() ?? ":0";
            return int.TryParse(endpointAddress[(endpointAddress.IndexOf(':') + 1)..], out int port) ? port : 0;
        }

        public static async Task<NodeStatus> Get(string name)
        {
            string machineName = Environment.MachineName;
            string token = AlgodToken(name);
            int port = AlgodPort(name);
            if (port != 0)
            {
                if (name == "algorand")
                    Shared.AlgoPort = port;
                else if (name == "voi")
                    Shared.VoiPort = port;
            }

            string serviceStatus = await Utils.ServiceStatus(name, WinService(name));

            NodeStatus nodeStatus = new()
            {
                MachineName = machineName,
                IsWindows = IsWindows(),
                ServiceStatus = serviceStatus,
                Port = port,
                Token = token,
            };

            if (name == "algorand")
            {
                // Reti Status
                string exePath = Path.Combine(Utils.appDataDir, "reti", IsWindows() ? "reti.exe" : "reti");
                string retiServiceStatus = await Utils.ServiceStatus("reti", "Reti Validator");

                string? version = null;
                if (File.Exists(exePath))
                {
                    version = await Utils.Exec(exePath, "--version");
                }

                string? exeStatus = null;
                if (retiServiceStatus == "Running")
                {
                    try
                    {
                        using HttpClient client = new();
                        var ready = await client.GetAsync("http://localhost:6260/ready");
                        exeStatus = ready.IsSuccessStatusCode ? "Running" : "Stopped";
                    }
                    catch
                    {
                        exeStatus = "Stopped";
                    }
                }

                DaemonStatus retiStatus = new()
                {
                    ServiceStatus = retiServiceStatus,
                    Version = version,
                    ExeStatus = exeStatus,
                };

                nodeStatus.RetiStatus = retiStatus;

                // Valar Status
                nodeStatus.ValarStatus = await ValarController.GetStatus();

                // Telemetry Status
                nodeStatus.TelemetryStatus = await Utils.Exec(diagcfgPath, "-d", DataDir(name), "telemetry", "status");
            }

            return nodeStatus;
        }

        // 3.x Windows services ran as LocalSystem with no data-dir ACL grant.
        // Upgrading to the virtual-account model leaves them unable to access the
        // SYSTEM-owned data dir, so they fail to start. On startup, reconfigure any
        // legacy service onto its per-service virtual account, grant the existing
        // data dir, and restart it. Self-limiting: once off LocalSystem it's a no-op.
        public static async Task MigrateWindowsServices()
        {
            if (!IsWindows()) return;
            foreach (string name in new[] { "algorand", "voi" })
            {
                string svc = WinService(name);
                if (!(await Utils.Sc("qc", svc)).Contains("LocalSystem")) continue;
                await Utils.Sc("config", svc, "obj=", $"NT SERVICE\\{svc}");
                await ApplyDirOwnership(name);
                await Utils.RestartWindowsService(svc);
            }
        }

        public static async Task CreateService(string name)
        {
            if (!Directory.Exists(DataDir(name)))
            {
                await ExtractTemplate(name);
            }

            if (IsWindows())
            {
                string wrapper = Path.Combine(AppContext.BaseDirectory, "Services", "NodeServiceV2.exe");
                // The service's command line: the wrapper exe plus the data dir, both quoted.
                string binPath = $"\"{wrapper}\" \"{DataDir(name)}\"";
                string serviceName = WinService(name);
                // Run under the auto-managed per-service virtual account instead of LocalSystem.
                await Utils.Sc("create", serviceName, "binPath=", binPath, "obj=", $"NT SERVICE\\{serviceName}", "start=", "auto");
                await ApplyDirOwnership(name);
            }
            else if (IsLinux())
            {
                string templatePath = Path.Combine(AppContext.BaseDirectory, "Templates", "node.service");
                string template = File.ReadAllText(templatePath);
                string service = template.Replace("__NAME__", name).Replace("__PARENTDIR__", Utils.NodeDataParent(name));
                File.WriteAllText($"/lib/systemd/system/{name}.service", service);
                await ApplyDirOwnership(name);
                await Utils.Systemctl("daemon-reload");
                await Utils.Systemctl("enable", name);
            }
            else if (IsMacOS())
            {
                string templatePath = Path.Combine(AppContext.BaseDirectory, "Templates", "func.node.plist");
                string template = File.ReadAllText(templatePath);
                string plist = template.Replace("__NAME__", name).Replace("__PARENTDIR__", Utils.NodeDataParent(name));
                File.WriteAllText($"/Library/LaunchDaemons/func.{name}.plist", plist);
                await ApplyDirOwnership(name);
                await Utils.Launchctl("bootstrap", "system", $"/Library/LaunchDaemons/func.{name}.plist");
            }
        }

        public static async Task ResetData(string name)
        {
            if (Directory.Exists(DataDir(name)))
            {
                Directory.Delete(DataDir(name), true);
            }
            await ExtractTemplate(name);
            await ApplyDirOwnership(name);
        }

        public static async Task<string> Catchup(string name, Catchup model)
        {
            string goalPath = Path.Combine(Utils.appDataDir, "bin", "goal");
            return await Utils.Exec(goalPath, "node", "catchup", $"{model.Round}#{model.Label}", "-d", DataDir(name));
        }

        public static async Task ControlService(string name, string cmd)
        {
            if (IsWindows())
            {
                if (cmd == "restart")
                {
                    await ControlService(name, "stop");
                    await ControlService(name, "start");
                    return;
                }
                await Utils.Sc(cmd, WinService(name));
            }
            else if (IsLinux())
            {
                if (cmd == "delete") File.Delete($"/lib/systemd/system/{name}.service");
                else await Utils.Systemctl(cmd, name);
                await Utils.Systemctl("daemon-reload");
            }
            else if (IsMacOS())
            {
                // KeepAlive keeps the job running while loaded, so a deliberate stop must
                // unload it (bootout); start re-loads it (bootstrap).
                string plist = $"/Library/LaunchDaemons/func.{name}.plist";
                if (cmd == "start") await Utils.Launchctl("bootstrap", "system", plist);
                else if (cmd == "stop") await Utils.Launchctl("bootout", $"system/func.{name}");
                else if (cmd == "restart") await Utils.Launchctl("kickstart", "-k", $"system/func.{name}");
                else if (cmd == "delete")
                {
                    await Utils.Launchctl("bootout", $"system/func.{name}");
                    File.Delete(plist);
                }
            }
        }

        public static async Task<string> GetConfig(string name)
        {
            if (!Directory.Exists(DataDir(name)))
            {
                await ExtractTemplate(name);
            }
            string configPath = Path.Combine(DataDir(name), "config.json");
            string config = File.ReadAllText(configPath);
            return config;
        }

        // The UI round-trips the whole config.json but only edits a handful of keys.
        // Accept only those, each validated, and require every other key to be unchanged,
        // so the endpoint cannot be used to rewrite arbitrary algod settings.
        [GeneratedRegex(@"^(localhost|[0-9.]*):[0-9]{1,5}$")]
        private static partial Regex EndpointAddress();

        private static readonly Dictionary<string, Action<JToken>> EditableKeys = new()
        {
            ["EndpointAddress"] = v =>
            {
                string s = v.Type == JTokenType.String ? v.Value<string>()! : throw new Exception("EndpointAddress must be a string");
                if (!EndpointAddress().IsMatch(s) || !int.TryParse(s[(s.IndexOf(':') + 1)..], out int port) || port < 1 || port > 65535)
                    throw new Exception("EndpointAddress must be host:port");
            },
            ["BaseLoggerDebugLevel"] = v =>
            {
                if (v.Type != JTokenType.Integer || v.Value<long>() < 0 || v.Value<long>() > 6)
                    throw new Exception("BaseLoggerDebugLevel must be 0-6");
            },
            ["EnableP2P"] = v => { if (v.Type != JTokenType.Boolean) throw new Exception("EnableP2P must be a boolean"); },
            ["EnableP2PHybridMode"] = v => { if (v.Type != JTokenType.Boolean) throw new Exception("EnableP2PHybridMode must be a boolean"); },
            ["DNSBootstrapID"] = v =>
            {
                string s = v.Type == JTokenType.String ? v.Value<string>()! : throw new Exception("DNSBootstrapID must be a string");
                if (s.Length > 512 || s.Any(char.IsControl)) throw new Exception("DNSBootstrapID is invalid");
            },
        };

        public static void SetConfig(string name, Config model)
        {
            string configPath = Path.Combine(DataDir(name), "config.json");
            var loadSettings = new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error };

            if (JToken.Parse(model.Json, loadSettings) is not JObject submitted)
                throw new Exception("Config must be a JSON object");

            JObject existing = [];
            try { existing = JObject.Parse(File.ReadAllText(configPath), loadSettings); } catch { }

            foreach (string key in submitted.Properties().Select(p => p.Name).Union(existing.Properties().Select(p => p.Name)))
            {
                if (EditableKeys.TryGetValue(key, out var validate))
                {
                    if (submitted.TryGetValue(key, out var value) && value.Type != JTokenType.Null) validate(value);
                }
                else if (!JToken.DeepEquals(submitted[key], existing[key]))
                {
                    throw new Exception($"Changing {key} is not supported");
                }
            }

            File.WriteAllText(configPath, submitted.ToString(Formatting.Indented));
        }

        public static async Task SetDir(string name, Dir model)
        {
            string requestParent = Utils.ValidateDataParent(model.Path);
            string currentPath = DataDir(name);
            string requestPath = Path.Combine(requestParent, name);
            if (string.Equals(Path.GetFullPath(requestPath), Path.GetFullPath(currentPath), StringComparison.OrdinalIgnoreCase))
                throw new Exception("Data directory is already at that location");
            if (Path.GetFullPath(requestPath).StartsWith(Path.GetFullPath(currentPath) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new Exception("New location must not be inside the current data directory");
            CopyFolder(currentPath, requestPath);
            string filePath = Path.Combine(Utils.appDataDir, $"{name}.data");
            File.WriteAllText(filePath, requestParent);
            Directory.Delete(currentPath, true);
            await ApplyDirOwnership(name);
        }

        public static void CopyFolder(string sourceFolder, string destFolder)
        {
            Directory.CreateDirectory(destFolder);
            string[] files = Directory.GetFiles(sourceFolder);
            foreach (string file in files)
            {
                string name = Path.GetFileName(file);
                string dest = Path.Combine(destFolder, name);
                File.Copy(file, dest);
            }
            string[] folders = Directory.GetDirectories(sourceFolder);
            foreach (string folder in folders)
            {
                string name = Path.GetFileName(folder);
                string dest = Path.Combine(destFolder, name);
                CopyFolder(folder, dest);
            }
        }

        public static async Task EnableTelemetry(string name)
        {
            string dataPath = DataDir(name);
            await Utils.Exec(diagcfgPath, "-d", dataPath, "telemetry", "endpoint", "-e", "https://tel.4160.nodely.io");
            await Utils.Exec(diagcfgPath, "-d", dataPath, "telemetry", "name", "-n", "anon");
            await Utils.Exec(diagcfgPath, "-d", dataPath, "telemetry", "enable");
            await ControlService(name, "restart");
        }

        public static async Task DisableTelemetry(string name)
        {
            await Utils.Exec(diagcfgPath, "-d", DataDir(name), "telemetry", "disable");
            await ControlService(name, "restart");
        }
    }
}
