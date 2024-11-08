using System.ComponentModel.DataAnnotations;

namespace AvmWinNode.Models
{
    public class Catchup
    {
        public required uint Round { get; set; }

        [RegularExpression("^[A-Z0-9]{52}$", ErrorMessage = "Invalid Label")]
        public required string Label { get; set; }
    }
}
