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
        // Whether the user asked to hear about this node's blocks (Settings, Notify on Blocks Created).
        public bool NotifyBlocks { get; set; }
        // The latest rounds this node's block won, oldest first. Sent whatever NotifyBlocks
        // says, so the helper can skip past them while notifications are off.
        public List<TrayBlock> Blocks { get; set; } = [];
    }

    public class TrayBlock
    {
        public long Round { get; set; }
        public DateTimeOffset Time { get; set; }
    }

    public class UpdateInfo
    {
        public string? Installed { get; set; }
        public string? Latest { get; set; }
        public bool Available { get; set; }
        // The user wants to hear about it: node updates are in notify mode, or the node is
        // adopted (its own installer updates it). False in auto mode (FUNC installs it) and manual mode.
        public bool Notify { get; set; }
        // Why the installed version can't follow the network; alerted whatever the mode.
        public string? Unsupported { get; set; }
    }

    public class TrayMessage
    {
        public ulong Id { get; set; }
        public required string Title { get; set; }
    }
}
