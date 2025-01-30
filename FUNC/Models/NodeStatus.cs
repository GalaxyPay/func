namespace FUNC.Models
{
    public class NodeStatus
    {
        public required string MachineName { get; set; }
        public required string ServiceStatus { get; set; }
        public required int Port { get; set; }
        public required string Token { get; set; }
        public string? TelemetryStatus { get; set; }
        public RetiStatus? RetiStatus { get; set; }
    }

    public class RetiStatus
    {
        public required string ServiceStatus { get; set; }
        public string? Version { get; set; }
        public string? ExeStatus { get; set; }
    }

}
