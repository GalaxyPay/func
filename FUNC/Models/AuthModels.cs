using System.ComponentModel.DataAnnotations;

namespace FUNC.Models
{
    public class AuthStatus
    {
        public required bool HasPassword { get; set; }
        public required bool SignedIn { get; set; }
    }

    public class Session
    {
        public required string Token { get; set; }
    }

    public class PasswordRequest
    {
        [Required]
        public required string Password { get; set; }
    }

    public class ChangePassword
    {
        [Required]
        public required string CurrentPassword { get; set; }

        [Required]
        public required string NewPassword { get; set; }
    }
}
