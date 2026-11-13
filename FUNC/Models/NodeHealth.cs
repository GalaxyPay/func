using FUNC.Health;

namespace FUNC.Models
{
    // What the dashboard shows about one node's health over one time range.
    public class NodeHealth
    {
        // Where the data stands: "ok", "noLog" (no node.log to read), "stale" (the log
        // isn't growing) or "noEvents" (it grows but holds no telemetry events).
        public required string Source { get; set; }
        // Whether algod's /metrics endpoint is answering (peer counts, traffic).
        public bool Metrics { get; set; }
        public string? Version { get; set; }
        public DateTimeOffset? StartedAt { get; set; }
        public long LastRound { get; set; }
        public DateTimeOffset? LastRoundAt { get; set; }
        // When the node last sent a vote for one of its participating accounts.
        public DateTimeOffset? LastVoteAt { get; set; }
        public double? PeersIn { get; set; }
        public double? PeersOut { get; set; }
        // Width of each point, and the points oldest first with no gaps.
        public int BucketSeconds { get; set; }
        public List<HealthPoint> Points { get; set; } = [];
        // Connected peers as algod last listed them (about hourly).
        public List<PeerInfo> Peers { get; set; } = [];
        public DateTimeOffset? PeersAt { get; set; }
        // Recent errors from the log, newest first.
        public List<HealthNote> Notes { get; set; } = [];
    }

    public class HealthPoint
    {
        // Start of the interval, Unix seconds.
        public long T { get; set; }
        public int Rounds { get; set; }
        // Time between consecutive rounds; null when none were measured.
        public double? RoundMs { get; set; }
        public double RoundMsMax { get; set; }
        public int Votes { get; set; }
        public int Proposals { get; set; }
        // Proposals the network chose as the round's block.
        public int Wins { get; set; }
        public int Blocks { get; set; }
        // How long after a round started its block arrived; null without blocks.
        public double? ArrivalMs { get; set; }
        public double ArrivalMsMax { get; set; }
        // Rounds that needed more than one attempt to reach agreement.
        public int Recoveries { get; set; }
        public int Connects { get; set; }
        public int Disconnects { get; set; }
        public int ConnectFails { get; set; }
        public int Warnings { get; set; }
        public int Errors { get; set; }
        public double? PeersIn { get; set; }
        public double? PeersOut { get; set; }
        public double BytesIn { get; set; }
        public double BytesOut { get; set; }
        // Agreement messages algod dropped because it couldn't keep up.
        public double Dropped { get; set; }
    }
}
