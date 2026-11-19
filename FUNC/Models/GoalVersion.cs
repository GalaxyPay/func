namespace FUNC.Models
{
    public class GoalVersion
    {
        public required string Installed { get; set; }
        public required string Latest { get; set; }
        // Who installs the algorand node's binaries: "func", or an adopted node's manager.
        public string Manager { get; set; } = "func";
        public required NodeUpdates Updates { get; set; }
        // Why the installed version can't follow the network (it will stall at an approved
        // consensus upgrade), or null if it can or that can't be determined.
        public string? Unsupported { get; set; }
    }
}
