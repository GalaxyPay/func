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
        internal static string DataDir(string name) => Ext(name)?.DataDir ?? Path.Combine(Utils.NodeDataParent(name), name);

        // The adopted apt/NodeKit/update.sh node standing in as FUNC's algorand node, if any.
        private static ExternalNode? Ext(string name) => name == "algorand" ? External.Load() : null;

        // Run one of the node's tools (goal, diagcfg): FUNC's own, or an adopted node's
        // as the node's account so files it writes keep their owner.
        private static async Task<string> Tool(string name, string tool, params string[] args)
        {
            if (Ext(name) is { } ext) return (await External.RunAs(ext.User, Path.Combine(ext.BinDir, tool), args)).Stdout;
            return await Utils.Exec(Path.Combine(Utils.appDataDir, "bin", tool), args);
        }

        private static async Task<string> ServiceStatus(string name)
        {
            if (Ext(name) is { } ext) return await External.Status(ext);
            string status = await Utils.ServiceStatus(name, WinService(name));
            // The algorand package installs a unit with the same name as FUNC's. Don't
            // report that node as FUNC's; External.Sync adopts it instead.
            if (IsLinux() && (status is "Running" or "Stopped") && !External.IsFuncUnit(name)) return "Not Found";
            return status;
        }

        private static async Task<bool?> StartsAtBoot(string name)
        {
            if (Ext(name) is { } ext) return await External.StartsAtBoot(ext);
            return await Utils.StartsAtBoot(name, WinService(name));
        }

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
            // An adopted node's files belong to its own account, which FUNC leaves alone.
            if (!Directory.Exists(dataDir) || Ext(name) != null) return;
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

        // The port algod listens on, from the node's config.json, or 0 if unknown. Nodes
        // installed by apt or NodeKit often don't set one, so algod picks it and records
        // it in algod.net.
        public static int AlgodPort(string name)
        {
            string? configText = null;
            try { configText = File.ReadAllText(Path.Combine(DataDir(name), "config.json")); } catch { }
            string endpointAddress = ":0";
            try { endpointAddress = JObject.Parse(configText!).GetValue("EndpointAddress")?.Value<string>() ?? ":0"; } catch { }
            int port = int.TryParse(endpointAddress[(endpointAddress.LastIndexOf(':') + 1)..], out int p) ? p : 0;
            if (port != 0) return port;
            string net = string.Empty;
            try { net = File.ReadAllText(Path.Combine(DataDir(name), "algod.net")).Trim(); } catch { }
            return int.TryParse(net[(net.LastIndexOf(':') + 1)..], out p) ? p : 0;
        }

        public static async Task<NodeStatus> Get(string name)
        {
            if (name == "algorand") await SyncExternal();
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

            string serviceStatus = await ServiceStatus(name);
            ExternalNode? ext = Ext(name);

            NodeStatus nodeStatus = new()
            {
                MachineName = machineName,
                IsWindows = IsWindows(),
                ServiceStatus = serviceStatus,
                StartsAtBoot = serviceStatus is "Running" or "Stopped" ? await StartsAtBoot(name) : null,
                Port = port,
                Token = token,
                External = ext,
                Detected = name == "algorand" && ext == null && serviceStatus == "Not Found" ? await External.Detect() : null,
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
                nodeStatus.TelemetryStatus = await Tool(name, "diagcfg", "-d", DataDir(name), "telemetry", "status");
            }

            return nodeStatus;
        }

        // 3.x Windows services ran as LocalSystem with no data-dir ACL grant.
        // Upgrading to the virtual-account model leaves them unable to access the
        // SYSTEM-owned data dir, so they fail to start. On startup, reconfigure any
        // legacy service onto its per-service virtual account, grant the existing
        // data dir, and restart it. Self-limiting: once off LocalSystem it's a no-op.
        //
        // Also (re)apply the crash-restart recovery actions, which services created
        // before they were added to CreateService lack. Idempotent.
        public static async Task MigrateWindowsServices()
        {
            if (!IsWindows()) return;
            foreach (string name in new[] { "algorand", "voi" })
            {
                string svc = WinService(name);
                string qc = await Utils.Sc("qc", svc);
                if (!qc.Contains("SERVICE_NAME")) continue;
                await SetWindowsRecovery(svc);
                if (!qc.Contains("LocalSystem")) continue;
                await Utils.Sc("config", svc, "obj=", $"NT SERVICE\\{svc}");
                await ApplyDirOwnership(name);
                await Utils.RestartWindowsService(svc);
            }
        }

        // The service wrapper exits non-zero when algod dies; have the SCM restart it.
        private static Task SetWindowsRecovery(string svc) =>
            Utils.Sc("failure", svc, "reset=", "86400", "actions=", "restart/30000/restart/30000/restart/30000");

        // Adopt or forget an existing apt/NodeKit/update.sh mainnet node (see External.Sync).
        public static Task SyncExternal() =>
            External.Sync(async () => await ServiceStatus("algorand") != "Not Found");

        public static async Task CreateService(string name)
        {
            if (name == "algorand" && !IsWindows())
            {
                if (Ext(name) != null) throw new Exception("FUNC already manages an existing node");
                // Never install over a node set up by apt, NodeKit or update.sh: the package
                // owns a unit with the same name, and two mainnet nodes would compete.
                ExternalNode? found = await External.Detect();
                if (found?.Network == "mainnet")
                    throw new Exception($"An existing Algorand node was found at {found.DataDir}. Use it, or remove it first.");
                if (found?.Manager is "apt" or "rpm")
                    throw new Exception($"The algorand package ({found.Network} node at {found.DataDir}) uses the same service name as FUNC's node. Remove it first.");
                // Adopted nodes bring their own binaries, so FUNC's may not be installed yet.
                if (!File.Exists(Path.Combine(Utils.appDataDir, "bin", "algod"))) await GoalController.Install("latest");
            }

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
                await SetWindowsRecovery(serviceName);
                await ApplyDirOwnership(name);
            }
            else if (IsLinux())
            {
                string templatePath = Path.Combine(AppContext.BaseDirectory, "Templates", "node.service");
                string template = File.ReadAllText(templatePath);
                string service = template.Replace("__NAME__", name).Replace("__PARENTDIR__", Utils.NodeDataParent(name));
                File.WriteAllText($"/lib/systemd/system/{name}.service", service);
                await ApplyDirOwnership(name);
                // An `apt remove algorand` done outside FUNC leaves algorand.service masked.
                await Utils.Systemctl("unmask", name);
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
            // Wiping an adopted node removes it and its install; FUNC's own node replaces it.
            if (Ext(name) is { } ext) await External.Wipe(ext);
            if (Directory.Exists(DataDir(name)))
            {
                Directory.Delete(DataDir(name), true);
            }
            await ExtractTemplate(name);
            await ApplyDirOwnership(name);
        }

        public static async Task<string> Catchup(string name, Catchup model)
        {
            return await Tool(name, "goal", "node", "catchup", $"{model.Round}#{model.Label}", "-d", DataDir(name));
        }

        public static async Task ControlService(string name, string cmd)
        {
            if (Ext(name) is { } ext)
            {
                await External.Control(ext, cmd);
            }
            else if (IsWindows())
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
            string configPath = Path.Combine(DataDir(name), "config.json");
            // apt and NodeKit data dirs may have no config.json; algod then uses defaults.
            if (Ext(name) != null) return File.Exists(configPath) ? File.ReadAllText(configPath) : "{}";
            if (!Directory.Exists(DataDir(name)))
            {
                await ExtractTemplate(name);
            }
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

        public static async Task SetConfig(string name, Config model)
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

            bool created = !File.Exists(configPath);
            File.WriteAllText(configPath, submitted.ToString(Formatting.Indented));
            if (created && Ext(name) is { User: { } user }) await Utils.Exec("chown", user, configPath);
        }

        // Where the node's data dir lives, as the parent directory the UI edits.
        public static string DataParent(string name) =>
            Ext(name) is { } ext ? Path.GetDirectoryName(External.RealDataDir(ext))! : Utils.NodeDataParent(name);

        public static async Task SetDir(string name, Dir model)
        {
            string requestParent = Utils.ValidateDataParent(model.Path);
            if (Ext(name) is { } ext)
            {
                await External.Move(ext, requestParent);
                return;
            }
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
            await Tool(name, "diagcfg", "-d", dataPath, "telemetry", "endpoint", "-e", "https://tel.4160.nodely.io");
            await Tool(name, "diagcfg", "-d", dataPath, "telemetry", "name", "-n", "anon");
            await Tool(name, "diagcfg", "-d", dataPath, "telemetry", "enable");
            await ControlService(name, "restart");
        }

        public static async Task DisableTelemetry(string name)
        {
            await Tool(name, "diagcfg", "-d", DataDir(name), "telemetry", "disable");
            await ControlService(name, "restart");
        }
    }
}
