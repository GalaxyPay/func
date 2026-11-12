using System.Globalization;
using System.Text.Json;

namespace FUNC.Health
{
    // Turns node.log lines into health events. algod logs one JSON object per line, with
    // keys in alphabetical order. Its telemetry events (the ones Nodely receives) are
    // logged too, with msg "/<Category>/<Event>" and their fields under "details".
    public static class LogParser
    {
        private const int MaxText = 300;

        // A mainnet node logs every vote it receives, hundreds of lines per round. Only
        // lines containing one of these are worth parsing as JSON.
        public static bool IsCandidate(ReadOnlySpan<byte> line) =>
            line.IndexOf("\"msg\":\"/"u8) >= 0
            || line.IndexOf("\"Type\":\"RoundConcluded\""u8) >= 0
            || line.IndexOf("\"level\":\"warning\""u8) >= 0
            || line.IndexOf("\"level\":\"error\""u8) >= 0
            || line.IndexOf("\"level\":\"fatal\""u8) >= 0
            || line.IndexOf("\"level\":\"panic\""u8) >= 0;

        public static HealthEvent? Parse(ReadOnlySpan<byte> line)
        {
            if (!IsCandidate(line)) return null;
            try
            {
                var reader = new Utf8JsonReader(line);
                using JsonDocument doc = JsonDocument.ParseValue(ref reader);
                JsonElement root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object) return null;
                if (!DateTimeOffset.TryParse(Str(root, "time"), CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTimeOffset time)) return null;

                string msg = Str(root, "msg") ?? "";
                if (msg.StartsWith('/') && Telemetry(root, msg, time) is { } ev) return ev;

                if (Str(root, "Type") == "RoundConcluded")
                    return new HealthEvent { Kind = HealthEventKind.RoundConcluded, Time = time, Round = Long(root, "Round"), Period = (int)Long(root, "Period") };

                return Str(root, "level") switch
                {
                    "warning" => new HealthEvent { Kind = HealthEventKind.Warning, Time = time, Text = Clip(msg) },
                    "error" or "fatal" or "panic" => new HealthEvent { Kind = HealthEventKind.Error, Time = time, Text = Clip(msg) },
                    _ => null,
                };
            }
            catch (JsonException)
            {
                // A truncated or non-JSON line.
                return null;
            }
        }

        private static HealthEvent? Telemetry(JsonElement root, string msg, DateTimeOffset time)
        {
            // Older releases put the event's fields at the top level.
            JsonElement d = root.TryGetProperty("details", out JsonElement details) && details.ValueKind == JsonValueKind.Object ? details : root;
            string? version = Str(root, "v");
            switch (msg[(msg.LastIndexOf('/') + 1)..])
            {
                case "BlockAccepted":
                    return new HealthEvent { Kind = HealthEventKind.BlockAccepted, Time = time, Round = Long(d, "Round"), ReceivedMs = Long(d, "ReceivedAt") / 1e6, Version = version };
                case "VoteSent":
                    return new HealthEvent { Kind = HealthEventKind.VoteSent, Time = time, Round = Long(d, "Round"), Period = (int)Long(d, "Period"), Version = version };
                case "BlockProposed":
                    return new HealthEvent { Kind = HealthEventKind.BlockProposed, Time = time, Round = Long(d, "Round"), Period = (int)Long(d, "Period"), Version = version };
                case "ConnectPeer":
                    return new HealthEvent { Kind = HealthEventKind.ConnectPeer, Time = time, Text = PeerName(d), Version = version };
                case "ConnectPeerFail":
                    return new HealthEvent { Kind = HealthEventKind.ConnectPeerFail, Time = time, Text = Reason(d), Version = version };
                case "DisconnectPeer":
                    return new HealthEvent { Kind = HealthEventKind.DisconnectPeer, Time = time, Text = Reason(d), Version = version };
                case "PeerConnections":
                    return new HealthEvent { Kind = HealthEventKind.PeerConnections, Time = time, Peers = [.. Peers(d, "IncomingPeers", true), .. Peers(d, "OutgoingPeers", false)], Version = version };
                case "Heartbeat":
                    return new HealthEvent { Kind = HealthEventKind.Heartbeat, Time = time, Metrics = Numbers(d, "m"), Version = version };
                case "Startup":
                    return new HealthEvent { Kind = HealthEventKind.Startup, Time = time, Version = Str(d, "Version") ?? version };
                case "CatchupStart":
                    return new HealthEvent { Kind = HealthEventKind.CatchupStart, Time = time, Round = Long(d, "StartRound"), Version = version };
                case "CatchupStop":
                    return new HealthEvent { Kind = HealthEventKind.CatchupStop, Time = time, Round = Long(d, "EndRound"), Version = version };
                default:
                    return null;
            }
        }

        private static string? Str(JsonElement e, string name) =>
            e.TryGetProperty(name, out JsonElement v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

        private static long Long(JsonElement e, string name) =>
            e.TryGetProperty(name, out JsonElement v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out long n) ? n : 0;

        private static string Clip(string text) => text.Length <= MaxText ? text : text[..MaxText] + "…";

        // A peer as the user knows it: the relay's DNS name if there is one, else its address.
        private static string PeerName(JsonElement d) =>
            Str(d, "Endpoint") is { Length: > 0 } endpoint ? endpoint : Str(d, "Address") ?? "peer";

        private static string Reason(JsonElement d) =>
            Str(d, "Reason") is { Length: > 0 } reason ? $"{PeerName(d)} ({Clip(reason)})" : PeerName(d);

        private static IEnumerable<PeerInfo> Peers(JsonElement d, string name, bool incoming)
        {
            if (!d.TryGetProperty(name, out JsonElement list) || list.ValueKind != JsonValueKind.Array) yield break;
            foreach (JsonElement p in list.EnumerateArray())
            {
                if (p.ValueKind != JsonValueKind.Object) continue;
                // HostName is "<telemetry GUID>:<name>"; only the name means anything to a user.
                string? host = Str(p, "HostName");
                if (host != null) host = host[(host.IndexOf(':') + 1)..];
                yield return new PeerInfo
                {
                    Endpoint = Str(p, "Endpoint"),
                    Address = Str(p, "Address"),
                    Host = string.IsNullOrEmpty(host) ? null : host,
                    Incoming = incoming,
                    ConnectedSeconds = Long(p, "ConnectionDuration"),
                };
            }
        }

        private static Dictionary<string, double>? Numbers(JsonElement d, string name)
        {
            if (!d.TryGetProperty(name, out JsonElement map) || map.ValueKind != JsonValueKind.Object) return null;
            Dictionary<string, double> result = [];
            foreach (JsonProperty p in map.EnumerateObject())
                if (p.Value.ValueKind == JsonValueKind.Number) result[p.Name] = p.Value.GetDouble();
            return result;
        }
    }
}
