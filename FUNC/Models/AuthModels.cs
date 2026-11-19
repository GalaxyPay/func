using System.ComponentModel.DataAnnotations;

namespace FUNC.Models
{
    public class AuthStatus
    {
        public required bool HasPassword { get; set; }
        public required bool SignedIn { get; set; }
        // Whether wallet sign-in is available: some account has participation keys here.
        public required bool WalletEnabled { get; set; }
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
        // Either the current password or a wallet signature proves it's the owner.
        public string? CurrentPassword { get; set; }

        public WalletProof? Wallet { get; set; }

        [Required]
        public required string NewPassword { get; set; }
    }

    public class Challenge
    {
        public required string Nonce { get; set; }
    }

    // An ARC-60 signData response over a SIWA payload, as use-wallet returns it, with
    // byte fields in base64.
    public class WalletProof
    {
        [Required]
        public required string Address { get; set; }

        // The SIWA JSON that was signed.
        [Required]
        public required string Data { get; set; }

        // The public key that signed: the account's Algorand auth-addr (the account's
        // own key if not rekeyed).
        [Required]
        public required string Signer { get; set; }

        [Required]
        public required string Domain { get; set; }

        [Required]
        public required string AuthenticatorData { get; set; }

        [Required]
        public required string Signature { get; set; }
    }
}
