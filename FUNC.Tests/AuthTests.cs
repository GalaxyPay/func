using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;

namespace FUNC.Tests;

[Collection("AppData")]
public class AuthTests
{
    private const string Password = "correct horse battery";
    private static readonly string SessionsPath = Path.Combine(TestEnv.DataDir, "sessions.json");

    // How Auth keys a session: only this hash is ever stored.
    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    [Fact]
    public void SetPassword_StoresAHashAndSignsIn()
    {
        string token = Auth.SetPassword(Password);

        Assert.True(Auth.HasPassword);
        Assert.True(Auth.VerifyPassword(Password));
        Assert.False(Auth.VerifyPassword(Password + "x"));
        Assert.False(Auth.VerifyPassword(null));
        Assert.True(Auth.ValidateSession(token));
        Assert.DoesNotContain(Password, File.ReadAllText(Auth.AuthPath));
    }

    [Fact]
    public void SetPassword_SurvivesAReload()
    {
        string token = Auth.SetPassword(Password);

        Auth.Load();

        Assert.True(Auth.VerifyPassword(Password));
        Assert.True(Auth.ValidateSession(token));
    }

    [Fact]
    public void SetPassword_RevokesEarlierSessions()
    {
        Auth.SetPassword(Password);
        string earlier = Auth.CreateSession();

        string current = Auth.SetPassword(Password + "2");

        Assert.False(Auth.ValidateSession(earlier));
        Assert.True(Auth.ValidateSession(current));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("1234567")]
    public void ValidatePassword_RejectsShortPasswords(string? password)
    {
        Assert.Contains("at least 8", Assert.Throws<Exception>(() => Auth.ValidatePassword(password)).Message);
    }

    [Fact]
    public void ValidatePassword_EnforcesTheLengthRange()
    {
        Auth.ValidatePassword(new string('a', Auth.MinPasswordLength));
        Auth.ValidatePassword(new string('a', Auth.MaxPasswordLength));
        Assert.Contains("at most 128", Assert.Throws<Exception>(() => Auth.ValidatePassword(new string('a', Auth.MaxPasswordLength + 1))).Message);
    }

    [Fact]
    public void Load_IgnoresAnIncompletePasswordRecord()
    {
        File.WriteAllText(Auth.AuthPath, """{"Salt":"","Hash":"","Iterations":1}""");

        Auth.Load();

        Assert.False(Auth.HasPassword);
        Assert.False(Auth.VerifyPassword(Password));
    }

    [Fact]
    public void Sessions_PersistOnlyAHashOfTheToken()
    {
        string token = Auth.CreateSession();

        string stored = File.ReadAllText(SessionsPath);
        Assert.DoesNotContain(token, stored);
        Assert.Contains(Hash(token), stored);
    }

    [Fact]
    public void Sessions_CanBeRevoked()
    {
        string a = Auth.CreateSession();
        string b = Auth.CreateSession();

        Auth.RevokeSession(a);
        Assert.False(Auth.ValidateSession(a));
        Assert.True(Auth.ValidateSession(b));

        Auth.RevokeAllSessions();
        Assert.False(Auth.ValidateSession(b));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-session")]
    public void ValidateSession_RejectsUnknownTokens(string? token)
    {
        Assert.False(Auth.ValidateSession(token));
    }

    [Fact]
    public void Load_DropsExpiredSessions()
    {
        var sessions = new Dictionary<string, DateTime>
        {
            [Hash("expired")] = DateTime.UtcNow.AddMinutes(-1),
            [Hash("current")] = DateTime.UtcNow.AddDays(1),
        };
        File.WriteAllText(SessionsPath, JsonSerializer.Serialize(sessions));

        Auth.Load();

        Assert.False(Auth.ValidateSession("expired"));
        Assert.True(Auth.ValidateSession("current"));
    }

    [Theory]
    [InlineData("Bearer abc", "abc")]
    [InlineData("bearer  abc ", "abc")]
    [InlineData("Bearer ", null)]
    [InlineData("Basic abc", null)]
    [InlineData(null, null)]
    public void BearerToken_ReadsTheAuthorizationHeader(string? header, string? expected)
    {
        var context = new DefaultHttpContext();
        if (header != null) context.Request.Headers.Authorization = header;

        Assert.Equal(expected, Auth.BearerToken(context.Request));
    }

    [Fact]
    public void IsLoopback_CoversMappedAddresses()
    {
        Assert.True(Auth.IsLoopback(IPAddress.Loopback));
        Assert.True(Auth.IsLoopback(IPAddress.IPv6Loopback));
        Assert.True(Auth.IsLoopback(IPAddress.Parse("::ffff:127.0.0.1")));
        Assert.False(Auth.IsLoopback(IPAddress.Parse("192.168.1.10")));
        Assert.False(Auth.IsLoopback(null));
    }

    [Fact]
    public void Lockout_StartsAtFiveFailuresAndDoubles()
    {
        // A loopback address of its own, so the network-wide cap stays out of it.
        IPAddress address = IPAddress.Parse("127.0.0.5");

        for (int i = 0; i < 4; i++) Auth.RecordFailure(address);
        Assert.Equal(0, Auth.LockoutSeconds(address));

        Auth.RecordFailure(address);
        Assert.InRange(Auth.LockoutSeconds(address), 25, 30);

        Auth.RecordFailure(address);
        Assert.InRange(Auth.LockoutSeconds(address), 55, 60);

        Auth.ClearFailures(address);
        Assert.Equal(0, Auth.LockoutSeconds(address));
    }

    [Fact]
    public void Lockout_CapsFailuresAcrossTheNetworkButNotLoopback()
    {
        // Ten failures, each from a different address, so no single address is locked out.
        for (int i = 1; i <= 10; i++) Auth.RecordFailure(IPAddress.Parse($"203.0.113.{i}"));

        Assert.InRange(Auth.LockoutSeconds(IPAddress.Parse("198.51.100.1")), 1, 600);
        Assert.Equal(0, Auth.LockoutSeconds(IPAddress.Parse("127.0.0.9")));
    }

    private static AuthorizationFilterContext FilterContext(string? bearer, params object[] metadata)
    {
        var http = new DefaultHttpContext();
        if (bearer != null) http.Request.Headers.Authorization = $"Bearer {bearer}";
        var action = new ActionDescriptor { EndpointMetadata = [.. metadata] };
        return new AuthorizationFilterContext(new ActionContext(http, new RouteData(), action), []);
    }

    [Fact]
    public void SessionFilter_RejectsRequestsWithoutASession()
    {
        var missing = FilterContext(null);
        var wrong = FilterContext("not-a-session");

        new SessionFilter().OnAuthorization(missing);
        new SessionFilter().OnAuthorization(wrong);

        Assert.Equal(401, Assert.IsType<ContentResult>(missing.Result).StatusCode);
        Assert.Equal(401, Assert.IsType<ContentResult>(wrong.Result).StatusCode);
    }

    [Fact]
    public void SessionFilter_AllowsAValidSession()
    {
        var context = FilterContext(Auth.CreateSession());

        new SessionFilter().OnAuthorization(context);

        Assert.Null(context.Result);
    }

    [Fact]
    public void SessionFilter_SkipsAnonymousActions()
    {
        var context = FilterContext(null, new AllowAnonymousAttribute());

        new SessionFilter().OnAuthorization(context);

        Assert.Null(context.Result);
    }
}
