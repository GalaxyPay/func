namespace FUNC.Health
{
    public enum HealthEventKind
    {
        RoundConcluded,
        BlockAccepted,
        VoteSent,
        BlockProposed,
        ConnectPeer,
        ConnectPeerFail,
        DisconnectPeer,
        PeerConnections,
        Heartbeat,
        Startup,
        CatchupStart,
        CatchupStop,
        Warning,
        Error,
    }

    // One node.log line the dashboard cares about.
    public sealed class HealthEvent
    {
        public HealthEventKind Kind { get; init; }
        public DateTimeOffset Time { get; init; }
        public long Round { get; init; }
        public int Period { get; init; }
        // BlockAccepted: how long after the round started the block arrived.
        public double ReceivedMs { get; init; }
        // Warning/Error: the message. Peer events: the peer and, on disconnect, why.
        public string? Text { get; init; }
        public string? Version { get; init; }
        // Heartbeat: algod's metric registry, the same values /metrics serves.
        public Dictionary<string, double>? Metrics { get; init; }
        public List<PeerInfo>? Peers { get; init; }
    }

    public sealed class PeerInfo
    {
        public string? Endpoint { get; set; }
        public string? Address { get; set; }
        public string? Host { get; set; }
        public bool Incoming { get; set; }
        public long ConnectedSeconds { get; set; }
    }

    // An entry in the dashboard's recent-events list.
    public sealed class HealthNote
    {
        public DateTimeOffset Time { get; set; }
        // "info", "warning" or "error".
        public string Level { get; set; } = "info";
        public string Text { get; set; } = "";
        // How many times in a row the node logged this; Time is the latest.
        public int Count { get; set; } = 1;
    }
}
