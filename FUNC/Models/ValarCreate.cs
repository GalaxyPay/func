using System.ComponentModel.DataAnnotations;

namespace FUNC.Models
{
    // The daemon.config file is rendered server-side from these fields; the daemon
    // eval()s parts of that file, so free-form config text is never accepted.
    public class ValarCreate
    {
        [Required, MinLength(1), MaxLength(100)]
        public required List<ulong> ValidatorAdIds { get; set; }

        [Required]
        public required string Mnemonic { get; set; }
    }
}
