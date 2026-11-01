namespace FUNC.Models
{
    public class GoalVersion
    {
        public required string Installed { get; set; }
        public required string Latest { get; set; }
        // Who installs the algorand node's binaries: "func", or an adopted node's manager.
        public string Manager { get; set; } = "func";
    }
}
