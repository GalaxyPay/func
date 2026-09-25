using System.ComponentModel.DataAnnotations;

namespace FUNC.Models
{
    // The .env file is rendered server-side from these fields; free-form env text is
    // never accepted.
    public class RetiCreate
    {
        [Range(1, long.MaxValue)]
        public required long ValidatorId { get; set; }

        [Range(1, int.MaxValue)]
        public required int NodeNum { get; set; }

        [Required]
        public required string Mnemonic { get; set; }
    }
}
