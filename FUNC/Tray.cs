using System.Buffers.Binary;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.Json.Nodes;
using FUNC.Controllers;
using FUNC.Health;
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
        private static readonly SemaphoreSlim funcGate = new(1, 1);
        private static readonly SemaphoreSlim messagesGate = new(1, 1);

        private static (DateTimeOffset At, string? Value) funcLatest;
        private static (DateTimeOffset At, string? Value) nodeLatest;
        private static readonly TimeSpan KeysTtl = TimeSpan.FromMinutes(5);
        private static (DateTimeOffset At, List<TrayMessage> Value) messages = (DateTimeOffset.MinValue, []);
        private static (DateTimeOffset At, List<TrayKey> Value) expiringKeys = (DateTimeOffset.MinValue, []);

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

                status.FuncUpdate = await FuncUpdate();

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
                            Notify = ext != null || NodeUpdater.Mode == NodeUpdateMode.Notify,
                            Unsupported = await Consensus.InstalledBlocker(),
                        };
                    }
                }
                catch { }

                status.Messages = status.NodeStatus == "Running" ? await Messages() : messages.Value;

                if (status.NodeStatus == "Running" && DateTimeOffset.UtcNow - expiringKeys.At > KeysTtl)
                {
                    try
                    {
                        List<TrayKey> keys = (await PartKeys.List("algorand"))
                            .Where(k => k.ExpiringSoon)
                            .Select(k => new TrayKey { Id = k.Id, Address = k.Address, Expires = k.Expires })
                            .ToList();
                        expiringKeys = (DateTimeOffset.UtcNow, keys);
                    }
                    catch { }
                }
                // A stopped node is its own alert; its keys are reported again once it runs.
                status.ExpiringKeys = status.NodeStatus == "Running" ? expiringKeys.Value : [];

                status.NotifyBlocks = Notifications.Blocks;
                status.Blocks = HealthCollector.Store("algorand").RecentWins()
                    .Select(w => new TrayBlock { Round = w.Round, Time = w.Time }).ToList();

                return status;
            }
            finally
            {
                gate.Release();
            }
        }

        // The running FUNC against its latest release, shared with the dashboard's Settings;
        // null until GitHub has answered once.
        public static async Task<UpdateInfo?> FuncUpdate()
        {
            await funcGate.WaitAsync();
            try
            {
                funcLatest = await Cached(funcLatest, ReleaseTtl, LatestFuncVersion);
            }
            finally
            {
                funcGate.Release();
            }
            string? version = FuncVersion();
            if (funcLatest.Value is not string latest) return null;
            return new UpdateInfo
            {
                Installed = version,
                Latest = latest,
                Available = version != null && NodeUpdater.IsNewer(latest, version),
            };
        }

        // Broadcast messages from the contract, read through this node and shared with the
        // dashboard. Unsynced nodes don't have the boxes yet; a failed read keeps the last list.
        public static async Task<List<TrayMessage>> Messages(bool refresh = false)
        {
            await messagesGate.WaitAsync();
            try
            {
                if (refresh || DateTimeOffset.UtcNow - messages.At > MessagesTtl)
                {
                    try { messages = (DateTimeOffset.UtcNow, await FetchMessages()); } catch { }
                }
                return messages.Value;
            }
            finally
            {
                messagesGate.Release();
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
                if (name == null) continue;
                byte[] nameBytes = Convert.FromBase64String(name);
                // Only message ids (8 bytes) matter; skip allowedSenders boxes before fetching.
                if (nameBytes.Length != 8) continue;
                // algod (5.1.1 at least) ignores values=true and lists names only, so read
                // each box on its own when the listing doesn't carry its value.
                string? value = box?["value"]?.GetValue<string>();
                if (value == null)
                {
                    JsonObject? single = await Diagnostics.AlgodGet(http, port,
                        $"/v2/applications/{MessagesAppId}/box?name=b64:{Uri.EscapeDataString(name)}", token);
                    value = single?["value"]?.GetValue<string>();
                }
                if (value == null) continue;
                TrayMessage? message = DecodeMessage(nameBytes, Convert.FromBase64String(value));
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
                int bodyOffset = BinaryPrimitives.ReadUInt16BigEndian(value.AsSpan(2, 2));
                int bodyLength = BinaryPrimitives.ReadUInt16BigEndian(value.AsSpan(bodyOffset, 2));
                string body = Encoding.UTF8.GetString(value, bodyOffset + 2, bodyLength);
                return new TrayMessage { Id = id, Title = title, Body = body };
            }
            catch (ArgumentException) { return null; }
        }
    }
}
