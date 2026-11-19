using System.Buffers.Binary;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.Json.Nodes;
using FUNC.Controllers;
using FUNC.Models;
using Octokit;

namespace FUNC
{
    // Builds the snapshot the tray / menu-bar helper polls. Release lookups hit the
    // GitHub API, so they're cached; each part fails on its own, never the whole call.
    public static class Tray
    {
        // On-chain broadcast messages; mirrors VITE_MESSAGES_APP_ID in webui/.env.
        public const ulong MessagesAppId = 3638419055;

        private static readonly TimeSpan ReleaseTtl = TimeSpan.FromMinutes(30);
        private static readonly TimeSpan MessagesTtl = TimeSpan.FromMinutes(5);
        private static readonly SemaphoreSlim gate = new(1, 1);

        private static (DateTimeOffset At, string? Value) funcLatest;
        private static (DateTimeOffset At, string? Value) nodeLatest;
        private static (DateTimeOffset At, List<TrayMessage> Value) messages = (DateTimeOffset.MinValue, []);

        public static string? FuncVersion() =>
            Assembly.GetEntryAssembly()?
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion?.Split('+')[0];

        // The latest FUNC release's version ("4.1.4").
        internal static async Task<string> LatestFuncVersion()
        {
            var client = new GitHubClient(new ProductHeaderValue("func"));
            var latest = await client.Repository.Release.GetLatest("GalaxyPay", "func");
            return latest.TagName.TrimStart('v');
        }

        public static async Task<TrayStatus> Collect()
        {
            await gate.WaitAsync();
            try
            {
                using var p = Process.GetCurrentProcess();
                string? version = FuncVersion();
                var status = new TrayStatus
                {
                    Version = version,
                    Started = p.StartTime.ToUniversalTime().ToString("O"),
                };

                funcLatest = await Cached(funcLatest, ReleaseTtl, LatestFuncVersion);
                if (funcLatest.Value != null)
                {
                    status.FuncUpdate = new UpdateInfo
                    {
                        Installed = version,
                        Latest = funcLatest.Value,
                        Available = version != null && NodeUpdater.IsNewer(funcLatest.Value, version),
                    };
                }

                try { status.NodeStatus = await Node.ServiceStatus("algorand"); } catch { }

                try
                {
                    ExternalNode? ext = External.Load();
                    string installed = await GoalController.InstalledVersion(ext?.BinDir ?? Path.Combine(Utils.appDataDir, "bin"));
                    nodeLatest = await Cached(nodeLatest, ReleaseTtl, GoalController.LatestVersion);
                    if (installed.Length > 0 && nodeLatest.Value != null)
                    {
                        status.NodeUpdate = new UpdateInfo
                        {
                            Installed = installed,
                            Latest = nodeLatest.Value,
                            Available = NodeUpdater.IsNewer(nodeLatest.Value, installed),
                            Automatic = ext == null && NodeUpdater.Status().Enabled,
                        };
                    }
                }
                catch { }

                if (status.NodeStatus == "Running" && DateTimeOffset.UtcNow - messages.At > MessagesTtl)
                {
                    try { messages = (DateTimeOffset.UtcNow, await FetchMessages()); } catch { }
                }
                status.Messages = messages.Value;

                return status;
            }
            finally
            {
                gate.Release();
            }
        }

        // A failed lookup keeps the last good value and is retried on the next poll.
        private static async Task<(DateTimeOffset, string?)> Cached(
            (DateTimeOffset At, string? Value) entry, TimeSpan ttl, Func<Task<string>> fetch)
        {
            if (entry.Value != null && DateTimeOffset.UtcNow - entry.At < ttl) return entry;
            try { return (DateTimeOffset.UtcNow, await fetch()); }
            catch { return entry; }
        }

        private static async Task<List<TrayMessage>> FetchMessages()
        {
            using HttpClient http = new() { Timeout = TimeSpan.FromSeconds(10) };
            int port = Node.AlgodPort("algorand");
            string token = Node.AlgodToken("algorand");
            JsonObject? resp = await Diagnostics.AlgodGet(http, port, $"/v2/applications/{MessagesAppId}/boxes?values=true", token);
            List<TrayMessage> result = [];
            if (resp?["boxes"] is not JsonArray boxes) return result;
            foreach (JsonNode? box in boxes)
            {
                string? name = box?["name"]?.GetValue<string>();
                string? value = box?["value"]?.GetValue<string>();
                if (name == null || value == null) continue;
                TrayMessage? message = DecodeMessage(Convert.FromBase64String(name), Convert.FromBase64String(value));
                if (message != null) result.Add(message);
            }
            return result.OrderBy(m => m.Id).ToList();
        }

        // Message boxes have 8-byte uint64 keys and an ABI (string,string) title/body
        // value; allowedSenders boxes have 32-byte address keys and are skipped.
        internal static TrayMessage? DecodeMessage(byte[] name, byte[] value)
        {
            if (name.Length != 8 || value.Length < 4) return null;
            try
            {
                ulong id = BinaryPrimitives.ReadUInt64BigEndian(name);
                int offset = BinaryPrimitives.ReadUInt16BigEndian(value.AsSpan(0, 2));
                int length = BinaryPrimitives.ReadUInt16BigEndian(value.AsSpan(offset, 2));
                string title = Encoding.UTF8.GetString(value, offset + 2, length);
                return new TrayMessage { Id = id, Title = title };
            }
            catch (ArgumentException) { return null; }
        }
    }
}
