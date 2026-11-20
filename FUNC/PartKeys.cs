using System.Text.Json.Nodes;
using FUNC.Models;

namespace FUNC
{
    // The node's participation keys with an estimated expiry, flagging the registered
    // keys the user has to renew themselves. Read by the dashboard and the tray alike.
    public static class PartKeys
    {
        public static readonly TimeSpan WarnWithin = TimeSpan.FromDays(7);
        private static readonly TimeSpan BlockTimeTtl = TimeSpan.FromHours(1);
        // Rounds averaged over to estimate the block time.
        private const ulong Sample = 1000;

        private static readonly Dictionary<string, (DateTimeOffset At, double Ms)> blockTimes = [];

        public static async Task<List<PartKey>> List(string name)
        {
            using HttpClient http = new() { Timeout = TimeSpan.FromSeconds(10) };
            int port = Node.AlgodPort(name);
            string token = Node.AlgodToken(name);
            if (port == 0 || token.Length == 0) return [];

            JsonArray keys = await AlgodGet(http, port, "/v2/participation", token, allowMissing: true) as JsonArray ?? [];
            if (keys.Count == 0) return [];

            // A failed lookup leaves the expiry unknown rather than failing the whole list.
            ulong lastRound = 0;
            try { lastRound = (await AlgodGet(http, port, "/v2/status", token))?["last-round"]?.GetValue<ulong>() ?? 0; } catch { }
            DateTimeOffset now = DateTimeOffset.UtcNow;
            double? blockMs = lastRound == 0 ? null : await BlockTime(name, http, port, token, lastRound);
            // null: not known yet whether Reti or Valar manages an account.
            Dictionary<string, string>? managed = name == "algorand" ? await ManagedAccounts.Get() : [];

            Dictionary<string, JsonObject?> accounts = [];
            List<PartKey> result = [];
            foreach (JsonNode? node in keys)
            {
                string? address = node?["address"]?.GetValue<string>();
                string? id = node?["id"]?.GetValue<string>();
                JsonObject? key = node?["key"] as JsonObject;
                if (address == null || id == null || key == null) continue;
                if (!accounts.TryGetValue(address, out JsonObject? account))
                {
                    try { account = await AlgodGet(http, port, $"/v2/accounts/{address}?exclude=all", token) as JsonObject; }
                    // Leave the key out: the dashboard then checks it itself, and the tray
                    // doesn't warn about a key it can't tell is registered.
                    catch { continue; }
                    accounts[address] = account;
                }

                ulong lastValid = key["vote-last-valid"]?.GetValue<ulong>() ?? 0;
                PartKey part = new()
                {
                    Id = id,
                    Address = address,
                    LastValid = lastValid,
                    Expires = blockMs is double ms
                        ? now.AddMilliseconds(((double)lastValid - lastRound) * ms)
                        : null,
                    Registered = IsRegistered(key, account?["participation"] as JsonObject),
                    Manager = AlgoAddress.KeyHex(address) is string hex ? managed?.GetValueOrDefault(hex) : null,
                };
                part.ExpiringSoon = part.Registered && managed != null && part.Manager == null
                    && part.Expires is DateTimeOffset at && at - now <= WarnWithin;
                result.Add(part);
            }
            return result;
        }

        // The account is online with exactly this key.
        internal static bool IsRegistered(JsonObject key, JsonObject? online)
        {
            if (online == null) return false;
            foreach (string field in (string[])["vote-participation-key", "selection-participation-key", "state-proof-key",
                "vote-first-valid", "vote-last-valid", "vote-key-dilution"])
            {
                if (online[field]?.ToJsonString() is not string value || value != key[field]?.ToJsonString()) return false;
            }
            return true;
        }

        // Milliseconds per round over the last Sample rounds, cached for an hour.
        private static async Task<double?> BlockTime(string name, HttpClient http, int port, string token, ulong lastRound)
        {
            lock (blockTimes)
            {
                if (blockTimes.TryGetValue(name, out var cached) && DateTimeOffset.UtcNow - cached.At < BlockTimeTtl) return cached.Ms;
            }
            if (lastRound <= Sample) return null;
            try
            {
                long newer = await Timestamp(http, port, token, lastRound);
                long older = await Timestamp(http, port, token, lastRound - Sample);
                double ms = (newer - older) * 1000.0 / Sample;
                if (ms <= 0) return null;
                lock (blockTimes) blockTimes[name] = (DateTimeOffset.UtcNow, ms);
                return ms;
            }
            catch
            {
                return null;
            }
        }

        private static async Task<long> Timestamp(HttpClient http, int port, string token, ulong round) =>
            (await AlgodGet(http, port, $"/v2/blocks/{round}?header-only=true", token))?["block"]?["ts"]?.GetValue<long>()
                ?? throw new Exception($"Block {round} has no timestamp");

        // A GET against this machine's algod; with allowMissing, 404 is null rather than an error.
        internal static async Task<JsonNode?> AlgodGet(HttpClient http, int port, string path, string token, bool allowMissing = false)
        {
            using HttpRequestMessage request = new(HttpMethod.Get, $"http://127.0.0.1:{port}{path}");
            request.Headers.Add("X-Algo-Api-Token", token);
            using HttpResponseMessage response = await http.SendAsync(request);
            if (allowMissing && response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
            string body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) throw new Exception($"{path} returned {(int)response.StatusCode}: {body.Trim()}");
            return JsonNode.Parse(body);
        }
    }
}
