using System.Net;
using FUNC.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FUNC.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class AuthController(ILogger<AuthController> logger, IConfiguration config) : ControllerBase
    {
        private readonly ILogger<AuthController> _logger = logger;

        private IPAddress? Address => HttpContext.Connection.RemoteIpAddress;

        // Hosts a wallet signature may be bound to: this computer's names and addresses,
        // the dev origins allowed by Cors:Origins, and any extra names in func.json, e.g.
        // a DNS name pointing at this computer:
        //   { "Auth": { "Hosts": [ "node.home.lan:3537" ] } }
        // Never the request's Host header: the client sets that, so it can't prove
        // which site the wallet signed for.
        private IEnumerable<string> Hosts()
        {
            foreach (string host in WalletAuth.LocalHosts()) yield return host;
            foreach (string host in config.GetSection("Auth:Hosts").Get<string[]>() ?? []) yield return host;
            foreach (string origin in config.GetSection("Cors:Origins").Get<string[]>() ?? [])
            {
                if (Uri.TryCreate(origin, UriKind.Absolute, out Uri? uri)) yield return uri.Authority;
            }
        }

        // GET: auth
        // What the UI needs to pick between first-run setup, sign-in and the app.
        [HttpGet]
        [AllowAnonymous]
        public async Task<ActionResult<AuthStatus>> Status()
        {
            return new AuthStatus
            {
                HasPassword = Auth.HasPassword,
                SignedIn = Auth.ValidateSession(Auth.BearerToken(Request)),
                WalletEnabled = await WalletAuth.Enabled(),
            };
        }

        // POST: auth/setup
        // First-run password. Only while no password exists.
        [HttpPost("setup")]
        [AllowAnonymous]
        public ActionResult<Session> Setup(PasswordRequest model)
        {
            try
            {
                if (Auth.HasPassword) return BadRequest("A password is already set");
                string token = Auth.SetPassword(model.Password);
                _logger.LogInformation("Password set from {Address}", Address);
                return new Session { Token = token };
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // POST: auth/login
        [HttpPost("login")]
        [AllowAnonymous]
        public ActionResult<Session> Login(PasswordRequest model)
        {
            if (!Auth.HasPassword) return BadRequest("No password has been set yet");
            int wait = Auth.LockoutSeconds(Address);
            if (wait > 0) return TooManyAttempts(wait);
            if (!Auth.VerifyPassword(model.Password))
            {
                Auth.RecordFailure(Address);
                _logger.LogWarning("Failed sign-in from {Address}", Address);
                return Unauthorized("Incorrect password");
            }
            Auth.ClearFailures(Address);
            return new Session { Token = Auth.CreateSession() };
        }

        // GET: auth/challenge
        // A single-use nonce for a wallet to sign (Sign-in with Algorand).
        [HttpGet("challenge")]
        [AllowAnonymous]
        public ActionResult<Challenge> GetChallenge()
        {
            return new Challenge { Nonce = WalletAuth.CreateChallenge() };
        }

        // POST: auth/wallet-login
        // Sign in with a wallet signature from an account with participation keys here.
        // Only once a password exists: first-run setup always chooses one.
        [HttpPost("wallet-login")]
        [AllowAnonymous]
        public async Task<ActionResult<Session>> WalletLogin(WalletProof model)
        {
            if (!Auth.HasPassword) return BadRequest("No password has been set yet");
            int wait = Auth.LockoutSeconds(Address);
            if (wait > 0) return TooManyAttempts(wait);
            try
            {
                string account = await WalletAuth.Verify(model, Hosts());
                Auth.ClearFailures(Address);
                _logger.LogInformation("Wallet sign-in by {Account} from {Address}", account, Address);
                return new Session { Token = Auth.CreateSession() };
            }
            catch (WalletAuth.WalletAuthException ex)
            {
                Auth.RecordFailure(Address);
                _logger.LogWarning("Failed wallet sign-in from {Address}: {Reason}", Address, ex.Message);
                return Unauthorized(ex.Message);
            }
        }

        // POST: auth/logout
        [HttpPost("logout")]
        public ActionResult Logout()
        {
            Auth.RevokeSession(Auth.BearerToken(Request));
            return Ok();
        }

        // POST: auth/logout-all
        // Sign out every browser and device.
        [HttpPost("logout-all")]
        public ActionResult LogoutAll()
        {
            Auth.RevokeAllSessions();
            return Ok();
        }

        // PUT: auth/password
        // Change the password, proven with the current password or, if that's
        // forgotten, a wallet signature. Signs out every other session; returns a new one.
        [HttpPut("password")]
        public async Task<ActionResult<Session>> ChangePassword(ChangePassword model)
        {
            try
            {
                if (model.Wallet != null)
                {
                    try { await WalletAuth.Verify(model.Wallet, Hosts()); }
                    catch (WalletAuth.WalletAuthException ex) { return Unauthorized(ex.Message); }
                }
                else if (!Auth.VerifyPassword(model.CurrentPassword))
                {
                    return Unauthorized("Current password is incorrect");
                }
                string token = Auth.SetPassword(model.NewPassword);
                return new Session { Token = token };
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        private ObjectResult TooManyAttempts(int wait)
        {
            Response.Headers.RetryAfter = wait.ToString();
            return StatusCode(429, $"Too many attempts. Try again in {wait} seconds");
        }
    }
}
