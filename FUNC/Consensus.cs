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
        public static async Task<Requirement?> AlgorandRequirement()
        {
            int port = Node.AlgodPort("algorand");
            string token = Node.AlgodToken("algorand");
            if (port == 0 || token.Length == 0) return null;
            try
            {
                using HttpClient c = new() { Timeout = TimeSpan.FromSeconds(3) };
                return FromStatus(await Diagnostics.AlgodGet(c, port, "/v2/status", token));
            }
            catch { return null; }
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
            req == null ? null : Blocker(await ReleaseProtocols(tag), req);

        internal static string? Blocker(HashSet<string>? protocols, Requirement req)
        {
            if (protocols == null) return null;
            if (!protocols.Contains(req.Current))
                return "Doesn't support the network's current consensus protocol";
            if (req.Next != null && !protocols.Contains(req.Next))
                return req.NextRound is { } r
                    ? $"Doesn't support the consensus upgrade at round {r:N0}"
                    : "Doesn't support the approved consensus upgrade";
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
