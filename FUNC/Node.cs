using FUNC.Models;
using Newtonsoft.Json.Linq;
using static System.OperatingSystem;

namespace FUNC
{
    public class Node
    {
        private static async Task ExtractTemplate(string name)
        {
            string templatePath = Path.Combine(AppContext.BaseDirectory, "Templates", $"{name}.tar");
            await Utils.ExecCmd($"tar -xf \"{templatePath}\" -C {Utils.nodeDataParent}");
        }

        public static async Task<NodeStatus> Get(string name)
        {
            int port = 0;
            string token = string.Empty;
            try { token = File.ReadAllText(Path.Combine(Utils.nodeDataParent, name, "algod.admin.token")); } catch { }
            bool p2p = false;
            string? configText = null;
            try { configText = File.ReadAllText(Path.Combine(Utils.nodeDataParent, name, "config.json")); } catch { }
            if (configText != null)
            {
                JObject config = JObject.Parse(configText);
                var endpointAddressToken = config.GetValue("EndpointAddress");
                string endpointAddress = endpointAddressToken?.Value<string>() ?? ":0";
                port = int.Parse(endpointAddress[(endpointAddress.IndexOf(":") + 1)..]);
                var enableP2PToken = config.GetValue("EnableP2P");
                var enableP2PHybridModeToken = config.GetValue("EnableP2PHybridMode");
                bool enableP2P = enableP2PToken != null && enableP2PToken.Value<bool>();
                bool enableP2PHybridMode = enableP2PHybridModeToken != null && enableP2PHybridModeToken.Value<bool>();
                if (enableP2P || enableP2PHybridMode) p2p = true;
            }

            string sc = string.Empty;

            if (IsWindows())
            {
                sc = await Utils.ExecCmd($"sc query \"{Utils.Cap(name)} Node\"");
            }
            else if (IsLinux())
            {
                sc = await Utils.ExecCmd($"systemctl show {name} --property=LoadState --property=ActiveState");
            }
            else if (IsMacOS())
            {
                sc = await Utils.ExecCmd($"launchctl list | grep -i func.{name} || echo none");
            }

            string serviceStatus = Utils.ParseServiceStatus(sc);

            NodeStatus nodeStatus = new()
            {
                ServiceStatus = serviceStatus,
                Port = port,
                Token = token,
                P2p = p2p,
            };

            if (name == "algorand")
            {
                // Reti Status
                string retiQuery = string.Empty;
                string exePath = Path.Combine(Utils.appDataDir, "reti", "reti");

                if (IsWindows())
                {
                    retiQuery = await Utils.ExecCmd("sc query \"Reti Validator\"");
                    exePath += ".exe";
                }
                else if (IsLinux())
                {
                    retiQuery = await Utils.ExecCmd($"systemctl show reti --property=LoadState --property=ActiveState");
                }
                else if (IsMacOS())
                {
                    sc = await Utils.ExecCmd($"launchctl list | grep -i func.reti || echo none");
                }

                string retiServiceStatus = Utils.ParseServiceStatus(retiQuery);

                string? version = null;
                if (File.Exists(exePath))
                {
                    version = await Utils.ExecCmd(exePath + " --version");
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

                RetiStatus retiStatus = new()
                {
                    ServiceStatus = retiServiceStatus,
                    Version = version,
                    ExeStatus = exeStatus,
                };

                nodeStatus.RetiStatus = retiStatus;
            }

            return nodeStatus;
        }

        public static async Task CreateService(string name)
        {
            if (!Directory.Exists(Path.Combine(Utils.nodeDataParent, name)))
            {
                await ExtractTemplate(name);
            }

            if (IsWindows())
            {
                string nodeDataDir = Path.Combine(Utils.nodeDataParent, name);
                string binPath = $"\\\"{Path.Combine(AppContext.BaseDirectory, "Services", "NodeServiceV2.exe")}\\\" \\\"{nodeDataDir}\\\"";
                await Utils.ExecCmd($"sc create \"{Utils.Cap(name)} Node\" binPath= \"{binPath}\" start= auto");
            }
            else if (IsLinux())
            {
                string templatePath = Path.Combine(AppContext.BaseDirectory, "Templates", "node.service");
                string template = File.ReadAllText(templatePath);
                string service = template.Replace("__NAME__", name).Replace("__PARENTDIR__", Utils.nodeDataParent);
                File.WriteAllText($"/lib/systemd/system/{name}.service", service);
                await Utils.ExecCmd($"systemctl daemon-reload");
                await Utils.ExecCmd($"systemctl enable {name}");
            }
            else if (IsMacOS())
            {
                string templatePath = Path.Combine(AppContext.BaseDirectory, "Templates", "func.node.plist");
                string template = File.ReadAllText(templatePath);
                string plist = template.Replace("__NAME__", name).Replace("__PARENTDIR__", Utils.nodeDataParent);
                File.WriteAllText($"/Library/LaunchDaemons/func.{name}.plist", plist);
                await Utils.ExecCmd($"launchctl bootstrap system /Library/LaunchDaemons/func.{name}.plist");
            }
        }

        public static async Task ResetData(string name)
        {
            if (Directory.Exists(Path.Combine(Utils.nodeDataParent, name)))
            {
                Directory.Delete(Path.Combine(Utils.nodeDataParent, name), true);
            }
            await ExtractTemplate(name);
        }

        public static async Task<string> Catchup(string name, Catchup model)
        {
            string cmd = $"{Path.Combine(Utils.appDataDir, "bin", "goal")} node catchup {model.Round}#{model.Label} -d {Path.Combine(Utils.nodeDataParent, name)}";
            return await Utils.ExecCmd(cmd);
        }

        public static async Task ControlService(string name, string cmd)
        {
            if (IsWindows())
            {
                await Utils.ExecCmd($"sc {cmd} \"{Utils.Cap(name)} Node\"");
            }
            else if (IsLinux())
            {
                if (cmd == "delete") await Utils.ExecCmd($"rm /lib/systemd/system/{name}.service");
                else await Utils.ExecCmd($"systemctl {cmd} {name}");
                await Utils.ExecCmd($"systemctl daemon-reload");
            }
            else if (IsMacOS())
            {
                if (cmd == "start") await Utils.ExecCmd($"launchctl kickstart system/func.{name}");
                else if (cmd == "stop") await Utils.ExecCmd($"launchctl kill 9 system/func.{name}");
                else if (cmd == "delete")
                {
                    await Utils.ExecCmd($"launchctl bootout system/func.{name}");
                    await Utils.ExecCmd($"rm /Library/LaunchDaemons/func.{name}.plist");
                }
            }
        }

        public static async Task<string> GetConfig(string name)
        {
            if (!Directory.Exists(Path.Combine(Utils.nodeDataParent, name)))
            {
                await ExtractTemplate(name);
            }
            string configPath = Path.Combine(Utils.nodeDataParent, name, "config.json");
            string config = File.ReadAllText(configPath);
            return config;
        }

        public static void SetConfig(string name, Config model)
        {
            using StreamWriter writer = new(Path.Combine(Utils.nodeDataParent, name, "config.json"), false);
            writer.Write(model.Json);
        }
    }
}
