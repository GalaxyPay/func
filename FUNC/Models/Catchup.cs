using System.ComponentModel.DataAnnotations;

namespace FUNC.Models
{
    public class Catchup
    {
        public required uint Round { get; set; }

        [RegularExpression("^[A-Z0-9]{52}$", ErrorMessage = "Invalid Label")]
        public required string Label { get; set; }
    }
}
