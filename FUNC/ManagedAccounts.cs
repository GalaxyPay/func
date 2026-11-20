using System.Buffers.Binary;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace FUNC
{
    // The accounts whose participation keys the Reti or Valar daemon on this machine looks
    // after, read from chain through this node using each daemon's own config. Reti renews
    // its pools' keys itself; a Valar key lasts exactly as long as its delegation contract.
    public static partial class ManagedAccounts
    {
        private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(10);
        private static readonly SemaphoreSlim gate = new(1, 1);
        // Value is null until a lookup has succeeded.
        private static (DateTimeOffset At, Dictionary<string, string>? Value) cache = (DateTimeOffset.MinValue, null);

        private static readonly string RetiEnv = Path.Combine(Utils.appDataDir, "reti", ".env");
        private static readonly string ValarConfig = Path.Combine(Utils.appDataDir, "valar", "daemon.config");

        // Reti registry app per network, as the daemon defaults to (nodemgr networks.go).
        private static readonly Dictionary<string, ulong> RetiRegistry = new()
        {
            ["mainnet-v1.0"] = 2714516089,
            ["testnet-v1.0"] = 734834614,
        };

        // ValidatorInfo box: config (242) + state (26) + pools 24 x 18 + tokenPayoutRatio (200),
        // then nodePoolAssignments: 8 nodes x 3 pool app ids.
        internal const int RetiBoxLength = 1092;
        private const int NodeAssignmentsOffset = 900;
        private const int MaxNodes = 8;
        private const int PoolsPerNode = 3;

        // Valar delegator contract states READY, SUBMITTED and LIVE: the daemon holds a key.
        private static readonly byte[] ValarKeyedStates = [0x03, 0x04, 0x05];

        // Public key (hex) to "reti" or "valar". Empty without either daemon; a failed
        // lookup keeps the last good answer, and is null (unknown) before there is one.
        public static async Task<Dictionary<string, string>?> Get()
        {
            await gate.WaitAsync();
            try
            {
                if (cache.Value != null && DateTimeOffset.UtcNow - cache.At < Ttl) return cache.Value;
                Dictionary<string, string> result = [];
                bool reti = File.Exists(RetiEnv), valar = File.Exists(ValarConfig);
                if (reti || valar)
                {
                    using HttpClient http = new() { Timeout = TimeSpan.FromSeconds(10) };
                    int port = Node.AlgodPort("algorand");
                    string token = Node.AlgodToken("algorand");
                    try
                    {
                        if (reti)
                            foreach (string key in await Reti(http, port, token)) result[key] = "reti";
                        if (valar)
                            foreach (string key in await Valar(http, port, token)) result[key] = "valar";
                    }
                    catch
                    {
                        // Node down or still syncing: keep what was known, try again next time.
                        return cache.Value;
                    }
                }
                cache = (DateTimeOffset.UtcNow, result);
                return result;
            }
            finally
            {
                gate.Release();
            }
        }

        private static async Task<List<string>> Reti(HttpClient http, int port, string token)
        {
            Dictionary<string, string> env = File.ReadAllLines(RetiEnv)
                .Select(l => l.Split('=', 2))
                .Where(p => p.Length == 2)
                .ToDictionary(p => p[0].Trim(), p => p[1].Trim());
            if (!ulong.TryParse(env.GetValueOrDefault("RETI_VALIDATORID"), out ulong validatorId)
                || !int.TryParse(env.GetValueOrDefault("RETI_NODENUM"), out int nodeNum))
                return [];

            ulong registry;
            if (!ulong.TryParse(env.GetValueOrDefault("RETI_APPID"), out registry))
            {
                string? genesis = (await PartKeys.AlgodGet(http, port, "/versions", token))?["genesis_id"]?.GetValue<string>();
                if (genesis == null || !RetiRegistry.TryGetValue(genesis, out registry)) return [];
            }

            byte[] name = new byte[9];
            name[0] = (byte)'v';
            BinaryPrimitives.WriteUInt64BigEndian(name.AsSpan(1), validatorId);
            JsonNode? box = await PartKeys.AlgodGet(http, port, $"/v2/applications/{registry}/box?name=b64:{Uri.EscapeDataString(Convert.ToBase64String(name))}", token, allowMissing: true);
            if (box?["value"]?.GetValue<string>() is not string value) return [];
            return RetiPoolApps(Convert.FromBase64String(value), nodeNum).Select(AlgoAddress.AppKeyHex).ToList();
        }

        // This node's pool app ids from a validator's box; empty if the layout isn't the known one.
        internal static List<ulong> RetiPoolApps(byte[] box, int nodeNum)
        {
            if (box.Length != RetiBoxLength || nodeNum < 1 || nodeNum > MaxNodes) return [];
            List<ulong> apps = [];
            for (int k = 0; k < PoolsPerNode; k++)
            {
                ulong app = BinaryPrimitives.ReadUInt64BigEndian(box.AsSpan(NodeAssignmentsOffset + ((nodeNum - 1) * PoolsPerNode + k) * 8));
                if (app != 0) apps.Add(app);
            }
            return apps;
        }

        private static async Task<List<string>> Valar(HttpClient http, int port, string token)
        {
            Match ids = AdIdList().Match(File.ReadAllText(ValarConfig));
            if (!ids.Success) return [];
            List<string> result = [];
            foreach (string id in ids.Groups[1].Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!ulong.TryParse(id, out ulong adId)) continue;
                // A deleted ad answers 404.
                if (GlobalBytes(await PartKeys.AlgodGet(http, port, $"/v2/applications/{adId}", token, allowMissing: true), "del_app_list") is not byte[] list)
                    continue;
                for (int i = 0; i + 8 <= list.Length; i += 8)
                {
                    ulong delco = BinaryPrimitives.ReadUInt64BigEndian(list.AsSpan(i));
                    if (delco == 0) continue;
                    JsonNode? app = await PartKeys.AlgodGet(http, port, $"/v2/applications/{delco}", token, allowMissing: true);
                    byte[]? state = GlobalBytes(app, "state");
                    byte[]? beneficiary = GlobalBytes(app, "del_beneficiary");
                    if (state is { Length: 1 } && ValarKeyedStates.Contains(state[0]) && beneficiary is { Length: 32 })
                        result.Add(Convert.ToHexStringLower(beneficiary));
                }
            }
            return result;
        }

        // A byte-slice global from GET /v2/applications/{id}.
        internal static byte[]? GlobalBytes(JsonNode? app, string key)
        {
            string b64Key = Convert.ToBase64String(Encoding.ASCII.GetBytes(key));
            if (app?["params"]?["global-state"] is not JsonArray state) return null;
            foreach (JsonNode? kv in state)
            {
                if (kv?["key"]?.GetValue<string>() != b64Key) continue;
                return kv["value"]?["bytes"]?.GetValue<string>() is string bytes ? Convert.FromBase64String(bytes) : null;
            }
            return null;
        }

        // validator_ad_id_list = [123, 456]
        [GeneratedRegex(@"validator_ad_id_list\s*=\s*\[([^\]]*)\]")]
        private static partial Regex AdIdList();
    }
}
