namespace FUNC.Models
{
    // GET {name}/keys: one participation key on the node and when it should run out.
    public class PartKey
    {
        public required string Id { get; set; }
        public required string Address { get; set; }
        public ulong LastValid { get; set; }
        // Estimated from the recent block time; null until that's known.
        public DateTimeOffset? Expires { get; set; }
        // The account's registered (online) key.
        public bool Registered { get; set; }
        // "reti" for a Reti pool account, "valar" for a Valar delegator: that daemon
        // looks after the account's keys. Null for everything else.
        public string? Manager { get; set; }
        // Registered, not managed, and due to expire within a week (or already has).
        public bool ExpiringSoon { get; set; }
    }
}
