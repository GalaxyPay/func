namespace FUNC.Models
{
    // An Algorand node installed outside FUNC (apt/rpm package, NodeKit, Homebrew or
    // update.sh) that FUNC detects and, once adopted, manages in place.
    public class ExternalNode
    {
        // How the node's binaries are installed and updated: apt, rpm, brew, updater or manual.
        public required string Manager { get; set; }
        // The data dir the node's service is configured with. May be a symlink FUNC left
        // behind after moving the data, so the service and NodeKit keep finding it.
        public required string DataDir { get; set; }
        public required string BinDir { get; set; }
        // systemd unit (Linux) or launchd label (macOS); null when algod runs without a service.
        public string? Service { get; set; }
        // Account algod runs as and that owns the data dir; null means root.
        public string? User { get; set; }
        public string? Network { get; set; }
    }
}
