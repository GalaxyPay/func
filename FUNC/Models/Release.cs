using System.ComponentModel.DataAnnotations;

namespace FUNC.Models
{
    public class Release
    {
        [MaxLength(20)]
        [RegularExpression("^[a-zA-Z0-9 .-]+$", ErrorMessage = "Invalid Name")]
        public required string Name { get; set; }
    }

}
