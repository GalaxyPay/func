using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace FUNC
{
    // Password login for the API. The API runs as root/SYSTEM and manages OS services,
    // so every controller action requires a signed-in session.
    //
    // - The operator chooses a password on first run, from any device that can reach
    //   the service. Until then nothing but the setup call works, so the window to
    //   claim a new install is the time between install and first open.
    // - The password is stored as a PBKDF2-SHA256 hash in <appDataDir>/auth.json.
    //   Deleting that file and restarting the service returns to first-run setup.
    // - A successful login returns a random session token, sent by the UI as
    //   "Authorization: Bearer <token>". Only a SHA-256 of each token is persisted in
    //   <appDataDir>/sessions.json, so that file cannot be used to sign in.
    // - Failed logins are rate limited per source address, plus a network-wide cap
    //   that a rotating attacker cannot dodge. Loopback is exempt from the cap so the
    //   owner can always sign in at the machine itself.
    // - Accounts with participation keys on this computer can also sign in with a
    //   wallet signature instead; see WalletAuth.
    // - A forgotten password is reset with `FUNC --reset-password` as admin/root
    //   (the tray's "Reset password…" runs it). It deletes both files, and the
    //   running service picks that up without a restart, back to first-run setup.
    public static class Auth
    {
        public static readonly string AuthPath = Path.Combine(Utils.appDataDir, "auth.json");
        private static readonly string SessionsPath = Path.Combine(Utils.appDataDir, "sessions.json");

        public const int MinPasswordLength = 8;
        public const int MaxPasswordLength = 128;
        private const int Iterations = 600_000;
        private const int SaltBytes = 16;
        private const int HashBytes = 32;
        private static readonly TimeSpan SessionLifetime = TimeSpan.FromDays(30);

        // Per address: after 5 failures, wait 30s, doubling per further failure, up to 1h.
        private const int LockoutThreshold = 5;
        private static readonly TimeSpan LockoutBase = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan LockoutMax = TimeSpan.FromHours(1);
        // Network-wide: at most 10 failed attempts per 10 minutes from non-loopback addresses.
        private const int GlobalThreshold = 10;
        private static readonly TimeSpan GlobalWindow = TimeSpan.FromMinutes(10);

        private sealed class PasswordRecord
        {
            public string Salt { get; set; } = "";
            public string Hash { get; set; } = "";
            public int Iterations { get; set; }
        }

        private sealed class Failure
        {
            public int Count;
            public DateTime LockedUntil;
        }

        private static readonly object _lock = new();
        private static PasswordRecord? _password;
        private static Dictionary<string, DateTime> _sessions = []; // sha256(token) -> expiry (UTC)
        private static readonly Dictionary<string, Failure> _failures = [];
        private static readonly Queue<DateTime> _globalFailures = new();

        // Write times of the files as last read or written, to notice outside changes.
        private static DateTime _authStamp;
        private static DateTime _sessionsStamp;

        public static bool HasPassword { get { lock (_lock) { RefreshLocked(); return _password != null; } } }

        public static void Load()
        {
            Directory.CreateDirectory(Utils.appDataDir);
            lock (_lock)
            {
                LoadPasswordLocked();
                LoadSessionsLocked();
            }
        }

        private static void LoadPasswordLocked()
        {
            _authStamp = Stamp(AuthPath);
            _password = ReadJson<PasswordRecord>(AuthPath);
            if (_password != null && (_password.Salt.Length == 0 || _password.Hash.Length == 0)) _password = null;
        }

        private static void LoadSessionsLocked()
        {
            _sessionsStamp = Stamp(SessionsPath);
            _sessions = ReadJson<Dictionary<string, DateTime>>(SessionsPath) ?? [];
            PruneSessions();
        }

        // Reload whichever file changed underneath the service, e.g. deleted by
        // `FUNC --reset-password`.
        private static void RefreshLocked()
        {
            if (Stamp(AuthPath) != _authStamp) LoadPasswordLocked();
            if (Stamp(SessionsPath) != _sessionsStamp) LoadSessionsLocked();
        }

        private static DateTime Stamp(string path) => File.Exists(path) ? File.GetLastWriteTimeUtc(path) : DateTime.MinValue;

        private static void SavePasswordLocked()
        {
            WriteJson(AuthPath, _password);
            _authStamp = Stamp(AuthPath);
        }

        private static void SaveSessionsLocked()
        {
            WriteJson(SessionsPath, _sessions);
            _sessionsStamp = Stamp(SessionsPath);
        }

        // `FUNC --reset-password`: forget the password and every session. Needs
        // admin/root, as the files are only writable by the service's account.
        public static int ResetFromCommandLine()
        {
            if (!Environment.IsPrivilegedProcess)
            {
                Console.Error.WriteLine(OperatingSystem.IsWindows()
                    ? "Run this from an administrator terminal."
                    : $"Run with sudo: sudo {Environment.ProcessPath ?? "/opt/func/FUNC"} --reset-password");
                return 1;
            }
            try
            {
                File.Delete(AuthPath);
                File.Delete(SessionsPath);
            }
            catch (DirectoryNotFoundException) { }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Password reset failed: {ex.Message}");
                return 1;
            }
            Console.WriteLine("Password reset. Open FUNC to choose a new one.");
            return 0;
        }

        // ---- Password ----

        public static void ValidatePassword(string? password)
        {
            if (password == null || password.Length < MinPasswordLength)
                throw new Exception($"Password must be at least {MinPasswordLength} characters");
            if (password.Length > MaxPasswordLength)
                throw new Exception($"Password must be at most {MaxPasswordLength} characters");
        }

        // Set (or replace) the password. Every existing session is revoked; the caller
        // receives a fresh one.
        public static string SetPassword(string password)
        {
            ValidatePassword(password);
            byte[] salt = RandomNumberGenerator.GetBytes(SaltBytes);
            byte[] hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashBytes);
            lock (_lock)
            {
                RefreshLocked();
                _password = new PasswordRecord
                {
                    Salt = Convert.ToBase64String(salt),
                    Hash = Convert.ToBase64String(hash),
                    Iterations = Iterations,
                };
                SavePasswordLocked();
                _sessions.Clear();
                return CreateSessionLocked();
            }
        }

        public static bool VerifyPassword(string? password)
        {
            PasswordRecord? record;
            lock (_lock)
            {
                RefreshLocked();
                record = _password;
            }
            if (record == null || password == null) return false;
            byte[] salt = Convert.FromBase64String(record.Salt);
            byte[] expected = Convert.FromBase64String(record.Hash);
            byte[] actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, record.Iterations, HashAlgorithmName.SHA256, expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }

        // ---- Sessions ----

        public static string CreateSession()
        {
            lock (_lock)
            {
                RefreshLocked();
                return CreateSessionLocked();
            }
        }

        private static string CreateSessionLocked()
        {
            string token = RandomNumberGenerator.GetHexString(64, lowercase: true);
            PruneSessions();
            _sessions[HashToken(token)] = DateTime.UtcNow + SessionLifetime;
            SaveSessionsLocked();
            return token;
        }

        public static bool ValidateSession(string? token)
        {
            if (string.IsNullOrEmpty(token)) return false;
            string key = HashToken(token);
            lock (_lock)
            {
                RefreshLocked();
                return _sessions.TryGetValue(key, out DateTime expiry) && expiry > DateTime.UtcNow;
            }
        }

        public static void RevokeSession(string? token)
        {
            if (string.IsNullOrEmpty(token)) return;
            lock (_lock)
            {
                RefreshLocked();
                if (_sessions.Remove(HashToken(token))) SaveSessionsLocked();
            }
        }

        public static void RevokeAllSessions()
        {
            lock (_lock)
            {
                _sessions.Clear();
                SaveSessionsLocked();
            }
        }

        private static void PruneSessions()
        {
            DateTime now = DateTime.UtcNow;
            foreach (string key in _sessions.Where(kv => kv.Value <= now).Select(kv => kv.Key).ToList())
                _sessions.Remove(key);
        }

        private static string HashToken(string token)
        {
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
        }

        // Session token from an Authorization: Bearer header, or null.
        public static string? BearerToken(HttpRequest request)
        {
            const string scheme = "Bearer ";
            string? header = request.Headers.Authorization.FirstOrDefault();
            if (header == null || !header.StartsWith(scheme, StringComparison.OrdinalIgnoreCase)) return null;
            string token = header[scheme.Length..].Trim();
            return token.Length == 0 ? null : token;
        }

        // ---- Login rate limiting ----

        public static bool IsLoopback(IPAddress? address)
        {
            if (address == null) return false;
            if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
            return IPAddress.IsLoopback(address);
        }

        private static string Key(IPAddress? address)
        {
            if (address == null) return "unknown";
            if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
            return address.ToString();
        }

        // Seconds the caller must wait before another attempt, or 0 if not locked out.
        public static int LockoutSeconds(IPAddress? address)
        {
            DateTime now = DateTime.UtcNow;
            lock (_lock)
            {
                double wait = 0;
                if (_failures.TryGetValue(Key(address), out Failure? f))
                    wait = Math.Max(wait, (f.LockedUntil - now).TotalSeconds);
                if (!IsLoopback(address))
                {
                    PruneGlobal(now);
                    if (_globalFailures.Count >= GlobalThreshold)
                        wait = Math.Max(wait, (_globalFailures.Peek() + GlobalWindow - now).TotalSeconds);
                }
                return wait > 0 ? (int)Math.Ceiling(wait) : 0;
            }
        }

        public static void RecordFailure(IPAddress? address)
        {
            DateTime now = DateTime.UtcNow;
            lock (_lock)
            {
                string key = Key(address);
                if (!_failures.TryGetValue(key, out Failure? f)) _failures[key] = f = new Failure();
                f.Count++;
                if (f.Count >= LockoutThreshold)
                {
                    double factor = Math.Pow(2, Math.Min(f.Count - LockoutThreshold, 8));
                    TimeSpan lockout = TimeSpan.FromSeconds(Math.Min(LockoutBase.TotalSeconds * factor, LockoutMax.TotalSeconds));
                    f.LockedUntil = now + lockout;
                }
                if (!IsLoopback(address))
                {
                    PruneGlobal(now);
                    _globalFailures.Enqueue(now);
                }
            }
        }

        public static void ClearFailures(IPAddress? address)
        {
            lock (_lock) _failures.Remove(Key(address));
        }

        private static void PruneGlobal(DateTime now)
        {
            while (_globalFailures.Count > 0 && _globalFailures.Peek() + GlobalWindow <= now)
                _globalFailures.Dequeue();
        }

        // ---- Storage ----

        private static T? ReadJson<T>(string path) where T : class
        {
            try { return JsonSerializer.Deserialize<T>(File.ReadAllText(path)); } catch { return null; }
        }

        private static void WriteJson<T>(string path, T value)
        {
            string tmp = path + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(value));
            File.Move(tmp, path, true);
        }
    }

    // Rejects any controller action without a valid session, unless the action is marked
    // [AllowAnonymous]. Static files and the algod reverse proxy (which algod protects
    // with its own token) are not controller actions and are unaffected.
    public class SessionFilter : IAuthorizationFilter
    {
        public void OnAuthorization(AuthorizationFilterContext context)
        {
            if (context.ActionDescriptor.EndpointMetadata.OfType<IAllowAnonymous>().Any()) return;
            if (Auth.ValidateSession(Auth.BearerToken(context.HttpContext.Request))) return;
            context.Result = new ContentResult
            {
                StatusCode = StatusCodes.Status401Unauthorized,
                Content = "Sign in required",
                ContentType = "text/plain",
            };
        }
    }
}
