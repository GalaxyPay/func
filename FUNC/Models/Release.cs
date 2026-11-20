using System.ComponentModel.DataAnnotations;

namespace FUNC.Models
{
    public class Release
    {
        [MaxLength(20)]
        [RegularExpression("^[a-z0-9.-]+$", ErrorMessage = "Invalid Name")]
        public required string Name { get; set; }
    }

    // A node release offered for install, and why it can't be, if it can't.
    public class ReleaseOption
    {
        public required string Name { get; set; }
        public string? Unsupported { get; set; }
    }

}
