using System.ComponentModel.DataAnnotations;

namespace AvmWinNode.Models
{
    public class Release
    {
        [MaxLength(20)]
        [RegularExpression("^[a-z0-9\\.\\-]+$", ErrorMessage = "Invalid Name")]
        public required string Name { get; set; }
    }

}
