using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace FUNC
{
    // Which go-algorand releases can follow the Algorand network's consensus protocol. A node
    // on a release that doesn't know the protocol in effect stalls immediately, and one that
    // doesn't know an approved upgrade stalls at the upgrade round, so neither is installable.
    public static partial class Consensus
    {
        // The protocols the network requires: the one in effect, and an approved upgrade
        // that hasn't taken effect yet.
        public record Requirement(string Current, string? Next, ulong? NextRound);

        // Protocol versions each release knows, by tag. A tag's source never changes, so
        // entries are kept for the life of the process. Failed lookups aren't cached.
        private static readonly ConcurrentDictionary<string, HashSet<string>> releaseProtocols = new();

        private static readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(10) };

        // What the algorand node reports, or null when it isn't reachable (then nothing is blocked).
        public static async Task<Requirement?> AlgorandRequirement() => FromStatus(await AlgorandStatus());

        // Why the installed node can't follow the network, or null if it can or the node isn't reachable.
        public static async Task<string?> InstalledBlocker() => InstalledBlocker(await AlgorandStatus());

        private static async Task<JsonObject?> AlgorandStatus()
        {
            int port = Node.AlgodPort("algorand");
            string token = Node.AlgodToken("algorand");
            if (port == 0 || token.Length == 0) return null;
            try { return await Diagnostics.AlgodGet(statusHttp, port, "/v2/status", token); }
            catch { return null; }
        }

        // A running algod accepts on localhost at once. A stopped one leaves the port closed,
        // and Windows retries a refused localhost connect for ~2s before failing, which every
        // caller (goal/version, the tray) would otherwise wait out.
        internal static readonly HttpClient statusHttp = new(new SocketsHttpHandler
        {
            // Far above a localhost connect (well under 1ms), above Windows' ~15.6ms timer tick.
            ConnectTimeout = TimeSpan.FromMilliseconds(50),
        })
        { Timeout = TimeSpan.FromSeconds(3) };

        // algod knows whether its own build supports an approved upgrade: next-version-supported
        // is false when it doesn't, and the node will stall at next-version-round.
        internal static string? InstalledBlocker(JsonObject? status)
        {
            if (status?["next-version-supported"] is not JsonValue s || !s.TryGetValue(out bool supported) || supported)
                return null;
            ulong? round = status["next-version-round"] is JsonValue v && v.TryGetValue(out ulong r) ? r : null;
            return round is { } at
                ? $"Doesn't support the consensus upgrade at round {at:N0}"
                : "Doesn't support the approved consensus upgrade";
        }

        // algod reports next-version equal to last-version when no upgrade is pending.
        internal static Requirement? FromStatus(JsonObject? status)
        {
            string? current = status?["last-version"]?.ToString();
            if (string.IsNullOrEmpty(current)) return null;
            string? next = status?["next-version"]?.ToString();
            if (string.IsNullOrEmpty(next) || next == current) return new(current, null, null);
            ulong? round = status?["next-version-round"] is JsonValue v && v.TryGetValue(out ulong r) ? r : null;
            return new(current, next, round);
        }

        // Why a release can't follow the network, or null if it can or that can't be determined.
        public static async Task<string?> Blocker(string tag, Requirement? req) =>
            await Missing(tag, req) is { } missing ? $"Doesn't support {missing}" : null;

        internal static string? Blocker(HashSet<string>? protocols, Requirement req) =>
            Missing(protocols, req) is { } missing ? $"Doesn't support {missing}" : null;

        // The protocol a release lacks ("the consensus upgrade at round 1,000"), or null if it
        // has them all or that can't be determined.
        public static async Task<string?> Missing(string tag, Requirement? req) =>
            req == null ? null : Missing(await ReleaseProtocols(tag), req);

        internal static string? Missing(HashSet<string>? protocols, Requirement req)
        {
            if (protocols == null) return null;
            if (!protocols.Contains(req.Current))
                return "the network's current consensus protocol";
            if (req.Next != null && !protocols.Contains(req.Next))
                return req.NextRound is { } r
                    ? $"the consensus upgrade at round {r:N0}"
                    : "the approved consensus upgrade";
            return null;
        }

        // Windows builds (go-algo-win) use upstream's tags, so upstream's source covers both.
        private static async Task<HashSet<string>?> ReleaseProtocols(string tag)
        {
            if (releaseProtocols.TryGetValue(tag, out var known)) return known;
            try
            {
                string source = await http.GetStringAsync(
                    $"https://raw.githubusercontent.com/algorand/go-algorand/{Uri.EscapeDataString(tag)}/protocol/consensus.go");
                var protocols = ParseProtocols(source);
                if (protocols.Count == 0) return null;
                releaseProtocols[tag] = protocols;
                return protocols;
            }
            catch { return null; }
        }

        // Each protocol version is declared as ConsensusVersion("<specs URL>") in protocol/consensus.go.
        internal static HashSet<string> ParseProtocols(string source) =>
            ProtocolUrl().Matches(source).Select(m => m.Groups[1].Value).ToHashSet();

        [GeneratedRegex("\"(https://github\\.com/[^\"\\s]+)\"")]
        private static partial Regex ProtocolUrl();
    }
}
