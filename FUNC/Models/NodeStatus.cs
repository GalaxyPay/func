namespace FUNC.Models
{
    public class NodeStatus
    {
        public required string MachineName { get; set; }
        public required bool IsWindows { get; set; }
        public required string ServiceStatus { get; set; }
        public required int Port { get; set; }
        public required string Token { get; set; }
        public string? TelemetryStatus { get; set; }
        public DaemonStatus? RetiStatus { get; set; }
        public DaemonStatus? ValarStatus { get; set; }
    }

    // Status of an add-on daemon service (Reti, Valar).
    public class DaemonStatus
    {
        public required string ServiceStatus { get; set; }
        public string? Version { get; set; }
        public string? ExeStatus { get; set; }
    }

}
