namespace FUNC.Models
{
    public class NodeStatus
    {
        public required string MachineName { get; set; }
        public required bool IsWindows { get; set; }
        public required string ServiceStatus { get; set; }
        // Whether the service starts again when the computer boots, even if it was
        // stopped; null when not installed or unknown.
        public bool? StartsAtBoot { get; set; }
        public required int Port { get; set; }
        public required string Token { get; set; }
        public string? TelemetryStatus { get; set; }
        public DaemonStatus? RetiStatus { get; set; }
        public DaemonStatus? ValarStatus { get; set; }
        // The adopted apt/NodeKit/update.sh node FUNC is managing, if any.
        public ExternalNode? External { get; set; }
        // An existing node found while FUNC has none of its own, offered for adoption.
        public ExternalNode? Detected { get; set; }
    }

    // Status of an add-on daemon service (Reti, Valar).
    public class DaemonStatus
    {
        public required string ServiceStatus { get; set; }
        public string? Version { get; set; }
        public string? ExeStatus { get; set; }
    }

}
