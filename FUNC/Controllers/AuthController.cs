using System.Net;
using FUNC.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FUNC.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class AuthController(ILogger<AuthController> logger) : ControllerBase
    {
        private readonly ILogger<AuthController> _logger = logger;

        private IPAddress? Address => HttpContext.Connection.RemoteIpAddress;

        // GET: auth
        // What the UI needs to pick between first-run setup, sign-in and the app.
        [HttpGet]
        [AllowAnonymous]
        public ActionResult<AuthStatus> Status()
        {
            return new AuthStatus
            {
                HasPassword = Auth.HasPassword,
                SignedIn = Auth.ValidateSession(Auth.BearerToken(Request)),
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
            if (wait > 0)
            {
                Response.Headers.RetryAfter = wait.ToString();
                return StatusCode(429, $"Too many attempts. Try again in {wait} seconds");
            }
            if (!Auth.VerifyPassword(model.Password))
            {
                Auth.RecordFailure(Address);
                _logger.LogWarning("Failed sign-in from {Address}", Address);
                return Unauthorized("Incorrect password");
            }
            Auth.ClearFailures(Address);
            return new Session { Token = Auth.CreateSession() };
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
        // Change the password. Signs out every other session; returns a new one.
        [HttpPut("password")]
        public ActionResult<Session> ChangePassword(ChangePassword model)
        {
            try
            {
                if (!Auth.VerifyPassword(model.CurrentPassword)) return Unauthorized("Current password is incorrect");
                string token = Auth.SetPassword(model.NewPassword);
                return new Session { Token = token };
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}
