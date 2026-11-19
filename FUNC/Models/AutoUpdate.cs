namespace FUNC.Models
{
    // Automatic updates of FUNC's own node binaries to the latest release.
    public class AutoUpdate
    {
        public bool Enabled { get; set; }
        public DateTimeOffset? LastCheck { get; set; }
        // Why the last check or update failed, cleared by the next one that succeeds.
        public string? LastError { get; set; }
        // The version the node was last auto-updated to since FUNC started.
        public string? LastUpdated { get; set; }
    }

    public class AutoUpdateSettings
    {
        public required bool Enabled { get; set; }
    }
}
