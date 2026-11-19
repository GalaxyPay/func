using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using FUNC.Models;

namespace FUNC
{
    // Sign-in with Algorand: an alternative to the password, using an ARC-60 wallet
    // signature (signData) over a SIWA payload.
    //
    // - The UI gets a single-use nonce (GET auth/challenge), puts it in a SIWA payload,
    //   and has the wallet sign it. ARC-60 signs ed25519(sha256(data) || sha256(authenticatorData)),
    //   where authenticatorData starts with sha256(domain), the page's host.
    // - The domain must be a host this service answers on. Wallets refuse to sign for
    //   a domain other than the page's own, so a signature obtained by another site
    //   can't be replayed here.
    // - Algorand only (the "algorand" node; Voi isn't involved). Nothing is stored:
    //   any account with participation keys on the node may sign in, checked live.
    // - Rekeys are per network, and only Algorand mainnet's count. A rekeyed account
    //   signs with its Algorand auth-addr (use-wallet puts it in the signer field),
    //   and an account rekeyed away can no longer sign with its original key.
    //   A Voi rekey makes no difference.
    public static class WalletAuth
    {
        private static readonly TimeSpan ChallengeLifetime = TimeSpan.FromMinutes(5);
        private const int MaxChallenges = 1000;
        private static readonly TimeSpan ParticipationCacheLifetime = TimeSpan.FromMinutes(1);
        private const string AlgorandNode = "algorand";

        private static readonly object _lock = new();
        private static readonly Dictionary<string, DateTime> _challenges = []; // nonce -> expiry (UTC)
        private static (DateTime At, List<string> Accounts)? _participation;

        // Where participation keys and auth addresses come from. Tests replace these.
        internal static Func<Task<List<string>>> ParticipationSource = ReadParticipation;
        internal static Func<string, Task<string?>> AuthAddressSource = ReadAuthAddress;

        // ---- Challenges ----

        public static string CreateChallenge()
        {
            string nonce = RandomNumberGenerator.GetHexString(64, lowercase: true);
            AddChallenge(nonce, DateTime.UtcNow + ChallengeLifetime);
            return nonce;
        }

        internal static void AddChallenge(string nonce, DateTime expiry)
        {
            lock (_lock)
            {
                PruneChallenges();
                // Anyone can ask for a challenge, so keep the set bounded.
                if (_challenges.Count >= MaxChallenges)
                    _challenges.Remove(_challenges.MinBy(kv => kv.Value).Key);
                _challenges[nonce] = expiry;
            }
        }

        private static bool ConsumeChallenge(string nonce)
        {
            lock (_lock)
            {
                return _challenges.Remove(nonce, out DateTime expiry) && expiry > DateTime.UtcNow;
            }
        }

        private static void PruneChallenges()
        {
            DateTime now = DateTime.UtcNow;
            foreach (string key in _challenges.Where(kv => kv.Value <= now).Select(kv => kv.Key).ToList())
                _challenges.Remove(key);
        }

        // ---- Participating accounts ----

        // Whether any account could sign in with a wallet right now.
        public static async Task<bool> Enabled()
        {
            try { return (await Participating()).Count > 0; } catch { return false; }
        }

        internal static void ClearCache()
        {
            lock (_lock) _participation = null;
        }

        private static async Task<List<string>> Participating()
        {
            lock (_lock)
            {
                if (_participation is { } cached && DateTime.UtcNow - cached.At < ParticipationCacheLifetime)
                    return cached.Accounts;
            }
            List<string> accounts = await ParticipationSource();
            lock (_lock) _participation = (DateTime.UtcNow, accounts);
            return accounts;
        }

        // Accounts with participation keys on the node.
        private static async Task<List<string>> ReadParticipation()
        {
            List<string> accounts = [];
            string token = Node.AlgodToken(AlgorandNode);
            int port = Node.AlgodPort(AlgorandNode);
            if (token.Length == 0 || port == 0) return accounts;
            using HttpClient http = new() { Timeout = TimeSpan.FromSeconds(5) };
            try
            {
                // algod answers 404 when the node has no keys.
                if (await AlgodGet(http, port, token, "/v2/participation") is JsonArray keys)
                {
                    foreach (JsonNode? key in keys)
                    {
                        if (key?["address"]?.GetValue<string>() is string address) accounts.Add(address);
                    }
                }
            }
            catch { } // Node not running: no wallet sign-in until it is.
            return accounts;
        }

        // The address that authorizes the account on Algorand: its auth-addr if
        // rekeyed, else itself. Null if the node can't say.
        private static async Task<string?> ReadAuthAddress(string address)
        {
            string token = Node.AlgodToken(AlgorandNode);
            int port = Node.AlgodPort(AlgorandNode);
            if (token.Length == 0 || port == 0) return null;
            using HttpClient http = new() { Timeout = TimeSpan.FromSeconds(5) };
            try
            {
                JsonNode? account = await AlgodGet(http, port, token, $"/v2/accounts/{Uri.EscapeDataString(address)}?exclude=all");
                if (account is not JsonObject obj) return null;
                return obj["auth-addr"]?.GetValue<string>() ?? address;
            }
            catch { return null; }
        }

        private static async Task<JsonNode?> AlgodGet(HttpClient http, int port, string token, string path)
        {
            using HttpRequestMessage request = new(HttpMethod.Get, $"http://127.0.0.1:{port}{path}");
            request.Headers.Add("X-Algo-Api-Token", token);
            using HttpResponseMessage response = await http.SendAsync(request);
            if (!response.IsSuccessStatusCode) return null;
            return JsonNode.Parse(await response.Content.ReadAsStringAsync());
        }

        // ---- Hosts ----

        private static readonly int[] UiPorts = [3536, 3537];

        // The hosts the UI can be opened at on this computer: its name, localhost and
        // each of its IP addresses, on FUNC's ports, as a browser's location.host
        // writes them. Not the request's Host header, which the client chooses.
        public static IEnumerable<string> LocalHosts()
        {
            HashSet<string> names = new(StringComparer.OrdinalIgnoreCase) { "localhost" };
            try
            {
                string machine = System.Net.Dns.GetHostName();
                names.Add(machine);
                if (!machine.Contains('.')) names.Add(machine + ".local");
            }
            catch { }
            try
            {
                foreach (var nic in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (nic.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up) continue;
                    foreach (var unicast in nic.GetIPProperties().UnicastAddresses)
                    {
                        System.Net.IPAddress ip = unicast.Address;
                        if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
                        {
                            ip = new System.Net.IPAddress(ip.GetAddressBytes()); // drop the %scope
                            names.Add($"[{ip}]");
                        }
                        else names.Add(ip.ToString());
                    }
                }
            }
            catch { }
            return names.SelectMany(name => UiPorts.Select(port => $"{name}:{port}"));
        }

        // ---- Verification ----

        public sealed class WalletAuthException(string message) : Exception(message);

        // Checks a signed SIWA payload. Returns the signed-in account, or throws
        // WalletAuthException with a message for the user. hosts are where the UI may
        // legitimately be open (LocalHosts, plus any configured).
        public static async Task<string> Verify(WalletProof proof, IEnumerable<string> hosts)
        {
            byte[] data = Base64(proof.Data, "data");
            byte[] signer = Base64(proof.Signer, "signer");
            byte[] authenticatorData = Base64(proof.AuthenticatorData, "authenticatorData");
            byte[] signature = Base64(proof.Signature, "signature");

            Siwa siwa = ParseSiwa(data);
            if (siwa.Type != "ed25519") throw new WalletAuthException("Only ed25519 accounts can sign in");
            if (siwa.AccountAddress != proof.Address) throw new WalletAuthException("The signed account doesn't match");

            // The domain binds the signature to this service.
            if (siwa.Domain != proof.Domain || !hosts.Contains(siwa.Domain, StringComparer.OrdinalIgnoreCase))
                throw new WalletAuthException("The signature is for a different site");
            if (authenticatorData.Length < 32 || !authenticatorData.AsSpan(0, 32).SequenceEqual(SHA256.HashData(Encoding.UTF8.GetBytes(siwa.Domain))))
                throw new WalletAuthException("The signature is for a different site");
            if (siwa.ExpirationTime is DateTime expires && expires <= DateTime.UtcNow)
                throw new WalletAuthException("The sign-in request expired. Try again");

            byte[] signed = [.. SHA256.HashData(data), .. SHA256.HashData(authenticatorData)];
            if (!Ed25519.Verify(signer, signed, signature)) throw new WalletAuthException("The signature is not valid");

            // Single use, and only after the signature checks out so junk can't burn a nonce.
            if (siwa.Nonce == null || !ConsumeChallenge(siwa.Nonce))
                throw new WalletAuthException("The sign-in request expired. Try again");

            string account = siwa.AccountAddress;
            if (!(await Participating()).Contains(account))
                throw new WalletAuthException("This account can't sign in here. Only accounts with participation keys on this node can");

            string? authAddress = await AuthAddressSource(account);
            if (authAddress == null) throw new WalletAuthException("Couldn't look up the account on the node. Try again");
            if (!PublicKey(authAddress).AsSpan().SequenceEqual(signer))
            {
                throw new WalletAuthException(authAddress == account
                    ? "The signature is from a different account"
                    : "The account is rekeyed. Sign with the account it's rekeyed to on Algorand");
            }

            return account;
        }

        private sealed record Siwa(string Domain, string AccountAddress, string? Nonce, string? Type, DateTime? ExpirationTime);

        private static Siwa ParseSiwa(byte[] data)
        {
            try
            {
                using JsonDocument doc = JsonDocument.Parse(data);
                JsonElement root = doc.RootElement;
                string? Str(string name) => root.TryGetProperty(name, out JsonElement e) && e.ValueKind == JsonValueKind.String ? e.GetString() : null;
                DateTime? expires = Str("expiration-time") is string exp
                    ? DateTime.Parse(exp, null, System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal)
                    : null;
                return new Siwa(
                    Str("domain") ?? throw new FormatException(),
                    Str("account_address") ?? throw new FormatException(),
                    Str("nonce"),
                    Str("type"),
                    expires);
            }
            catch
            {
                throw new WalletAuthException("The signed sign-in request is malformed");
            }
        }

        private static byte[] Base64(string? value, string field)
        {
            try { return Convert.FromBase64String(value ?? ""); }
            catch { throw new WalletAuthException($"Invalid {field}"); }
        }

        // The ed25519 public key in an Algorand address: base32 (no padding) of the
        // 32-byte key followed by a 4-byte checksum.
        internal static byte[] PublicKey(string address)
        {
            const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
            if (address.Length != 58) throw new WalletAuthException("Invalid address");
            byte[] bytes = new byte[36];
            int buffer = 0, bits = 0, index = 0;
            foreach (char c in address)
            {
                int v = alphabet.IndexOf(c);
                if (v < 0) throw new WalletAuthException("Invalid address");
                buffer = (buffer << 5) | v;
                bits += 5;
                if (bits >= 8)
                {
                    bits -= 8;
                    if (index < bytes.Length) bytes[index++] = (byte)(buffer >> bits);
                    buffer &= (1 << bits) - 1;
                }
            }
            return bytes[..32];
        }
    }
}
