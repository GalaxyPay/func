namespace FUNC.Models
{
    // How FUNC's own node binaries are kept up to date. Adopted nodes ignore it: their
    // package manager or update.sh owns their binaries, and FUNC only alerts.
    public static class NodeUpdateMode
    {
        // FUNC installs each new release on its own.
        public const string Auto = "auto";
        // FUNC alerts when a new release is out; the user installs it.
        public const string Notify = "notify";
        // The node stays on the version the user picked, with no update alerts.
        public const string Manual = "manual";

        public static bool IsValid(string? mode) => mode is Auto or Notify or Manual;
    }

    public class NodeUpdates
    {
        public required string Mode { get; set; }
        public DateTimeOffset? LastCheck { get; set; }
        // Why the last auto-update check or install failed, cleared by the next one that succeeds.
        public string? LastError { get; set; }
        // The version the node was last auto-updated to since FUNC started.
        public string? LastUpdated { get; set; }
    }

    public class NodeUpdatesSettings
    {
        public required string Mode { get; set; }
    }
}
