namespace FUNC.Models
{
    // What the per-user tray / menu-bar helper polls from GET func/tray.
    public class TrayStatus
    {
        public string? Version { get; set; }
        public required string Started { get; set; }
        public UpdateInfo? FuncUpdate { get; set; }
        public UpdateInfo? NodeUpdate { get; set; }
        public string? NodeStatus { get; set; }
        public List<TrayMessage> Messages { get; set; } = [];
    }

    public class UpdateInfo
    {
        public string? Installed { get; set; }
        public string? Latest { get; set; }
        public bool Available { get; set; }
        // FUNC installs it on its own (node auto-update), so there's nothing to do.
        public bool Automatic { get; set; }
    }

    public class TrayMessage
    {
        public ulong Id { get; set; }
        public required string Title { get; set; }
    }
}
