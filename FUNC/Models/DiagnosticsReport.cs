using System.Text.Json.Nodes;

namespace FUNC.Models
{
    // A support snapshot of FUNC and its nodes, safe to paste into a support chat. Every
    // field is chosen on purpose; nothing here is copied wholesale from a file or another
    // model, so a secret added elsewhere can't leak into it.
    public class DiagnosticsReport
    {
        public DateTimeOffset GeneratedAt { get; set; } = DateTimeOffset.UtcNow;
        public required FuncDiagnostics Func { get; set; }
        // Always "algorand"; "voi" only when a Voi node is installed.
        public Dictionary<string, NodeDiagnostics> Nodes { get; set; } = [];
    }

    public class FuncDiagnostics
    {
        public string? Version { get; set; }
        public DateTimeOffset? StartedAt { get; set; }
        public string? Os { get; set; }
        public string? Arch { get; set; }
        public string? Dotnet { get; set; }
        public bool PasswordSet { get; set; }
        public bool CorsConfigured { get; set; }
        public string? AppDataDir { get; set; }
    }

    public class NodeDiagnostics
    {
        public string? ServiceStatus { get; set; }
        public bool? StartsAtBoot { get; set; }
        // "func", or how an adopted node is installed: apt, rpm, brew, updater or manual.
        public string? ManagedBy { get; set; }
        public string? Network { get; set; }
        // How an existing node FUNC found but hasn't adopted is installed.
        public string? DetectedExternal { get; set; }
        public bool? TelemetryEnabled { get; set; }
        public AlgodDiagnostics? Algod { get; set; }
        public HealthDiagnostics? Health { get; set; }
        public Dictionary<string, JsonNode?>? Config { get; set; }
        public DataDirDiagnostics? DataDir { get; set; }
        public RecentLogDiagnostics? RecentLog { get; set; }
        public DaemonStatus? Reti { get; set; }
        public DaemonStatus? Valar { get; set; }
        // A probe that failed, as "<section>: <message>". The rest of the report still fills in.
        public List<string> Errors { get; set; } = [];
    }

    public class AlgodDiagnostics
    {
        public int Port { get; set; }
        public bool TokenPresent { get; set; }
        // algod's build, from GET /versions.
        public string? Version { get; set; }
        public string? Channel { get; set; }
        public string? GenesisId { get; set; }
        // algod's GET /v2/status (rounds, catchup and protocol state; nothing secret).
        public JsonObject? Status { get; set; }
    }

    public class HealthDiagnostics
    {
        public required string Source { get; set; }
        public bool Metrics { get; set; }
        public DateTimeOffset? StartedAt { get; set; }
        public long LastRound { get; set; }
        public DateTimeOffset? LastRoundAt { get; set; }
        public DateTimeOffset? LastVoteAt { get; set; }
        public double? PeersIn { get; set; }
        public double? PeersOut { get; set; }
        public int Warnings24h { get; set; }
        public int Errors24h { get; set; }
    }

    public class DataDirDiagnostics
    {
        public string? Path { get; set; }
        public bool Exists { get; set; }
        public long? FreeBytes { get; set; }
        public long? TotalBytes { get; set; }
        public long? NodeLogBytes { get; set; }
        public DateTimeOffset? NodeLogModifiedAt { get; set; }
    }

    public class RecentLogDiagnostics
    {
        public bool Found { get; set; }
        // Warnings and errors in the last 24 hours (of those searched).
        public int Warnings { get; set; }
        public int Errors { get; set; }
        public List<LogSummary> Messages { get; set; } = [];
    }

    // One distinct warning or error message, with how often it appeared.
    public class LogSummary
    {
        public required string Level { get; set; }
        public required string Msg { get; set; }
        public int Count { get; set; }
        public DateTimeOffset? FirstAt { get; set; }
        public DateTimeOffset? LastAt { get; set; }
    }
}
