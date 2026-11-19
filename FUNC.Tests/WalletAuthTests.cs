using System.Text;
using System.Text.Json;
using FUNC.Controllers;
using FUNC.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace FUNC.Tests;

public class Ed25519Tests
{
    private static byte[] Hex(string hex) => Convert.FromHexString(hex);

    // RFC 8032 section 7.1, tests 1-3.
    public static TheoryData<string, string, string> Rfc8032 => new()
    {
        {
            "d75a980182b10ab7d54bfed3c964073a0ee172f3daa62325af021a68f707511a", "",
            "e5564300c360ac729086e2cc806e828a84877f1eb8e5d974d873e065224901555fb8821590a33bacc61e39701cf9b46bd25bf5f0595bbe24655141438e7a100b"
        },
        {
            "3d4017c3e843895a92b70aa74d1b7ebc9c982ccf2ec4968cc0cd55f12af4660c", "72",
            "92a009a9f0d4cab8720e820b5f642540a2b27b5416503f8fb3762223ebdb69da085ac1e43e15996e458f3613d0f11d8c387b2eaeb4302aeeb00d291612bb0c00"
        },
        {
            "fc51cd8e6218a1a38da47ed00230f0580816ed13ba3303ac5deb911548908025", "af82",
            "6291d657deec24024827e69c3abe01a30ce548a284743a445e3680d7db5ac3ac18ff9b538d16f290ae67f760984dc6594a7c15e9716ed28dc027beceea1ec40a"
        },
    };

    [Theory]
    [MemberData(nameof(Rfc8032))]
    public void Verify_AcceptsTheRfcVectors(string publicKey, string message, string signature)
    {
        Assert.True(Ed25519.Verify(Hex(publicKey), Hex(message), Hex(signature)));
    }

    [Theory]
    [MemberData(nameof(Rfc8032))]
    public void Verify_RejectsAnyChange(string publicKey, string message, string signature)
    {
        byte[] pk = Hex(publicKey), msg = Hex(message), sig = Hex(signature);

        Assert.False(Ed25519.Verify(pk, [.. msg, 0x00], sig));
        for (int i = 0; i < sig.Length; i += 7)
        {
            byte[] bad = (byte[])sig.Clone();
            bad[i] ^= 0x01;
            Assert.False(Ed25519.Verify(pk, msg, bad));
        }
        byte[] otherKey = (byte[])pk.Clone();
        otherKey[0] ^= 0x01;
        Assert.False(Ed25519.Verify(otherKey, msg, sig));
    }

    [Fact]
    public void Verify_RejectsANonCanonicalS()
    {
        (string pk, string msg, string sig) = ("d75a980182b10ab7d54bfed3c964073a0ee172f3daa62325af021a68f707511a", "",
            "e5564300c360ac729086e2cc806e828a84877f1eb8e5d974d873e065224901555fb8821590a33bacc61e39701cf9b46bd25bf5f0595bbe24655141438e7a100b");
        byte[] signature = Hex(sig);
        // s + L encodes the same scalar, which a malleable verifier would accept.
        System.Numerics.BigInteger l = System.Numerics.BigInteger.Pow(2, 252) + System.Numerics.BigInteger.Parse("27742317777372353535851937790883648493");
        System.Numerics.BigInteger s = new System.Numerics.BigInteger(signature.AsSpan(32), isUnsigned: true) + l;
        byte[] sBytes = new byte[32];
        s.TryWriteBytes(sBytes, out _, isUnsigned: true);
        sBytes.CopyTo(signature, 32);

        Assert.False(Ed25519.Verify(Hex(pk), Hex(msg), signature));
    }

    [Fact]
    public void Verify_RejectsWrongLengths()
    {
        Assert.False(Ed25519.Verify(new byte[31], [], new byte[64]));
        Assert.False(Ed25519.Verify(new byte[32], [], new byte[63]));
    }
}

// Fixtures/wallet-signin.json holds ARC-60 responses signed with fixed test keys;
// Fixtures/wallet-signin.mjs regenerates it.
[Collection("AppData")]
public class WalletAuthTests
{
    private const string Password = "correct horse battery";

    private sealed record Fixture(string Nonce, string Domain, WalletProof Direct, RekeyedProof Rekeyed);
    private sealed class RekeyedProof : WalletProof
    {
        public required string AuthAddr { get; set; }
    }

    private static readonly Fixture F = JsonSerializer.Deserialize<Fixture>(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "wallet-signin.json")),
        new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

    private readonly string[] _hosts = [F.Domain];
    // Accounts with participation keys on the Algorand node, and Algorand rekeys.
    private readonly List<string> _participating = [];
    private readonly Dictionary<string, string> _authAddresses = [];

    public WalletAuthTests()
    {
        WalletAuth.ClearCache();
        WalletAuth.ParticipationSource = () => Task.FromResult(_participating.ToList());
        WalletAuth.AuthAddressSource = address =>
            Task.FromResult<string?>(_authAddresses.TryGetValue(address, out string? auth) ? auth : address);
        WalletAuth.AddChallenge(F.Nonce, DateTime.UtcNow.AddMinutes(5));
        _participating.Add(F.Direct.Address);
        _participating.Add(F.Rekeyed.Address);
        _authAddresses[F.Rekeyed.Address] = F.Rekeyed.AuthAddr;
    }

    private static WalletProof Copy(WalletProof p) => new()
    {
        Address = p.Address,
        Data = p.Data,
        Signer = p.Signer,
        Domain = p.Domain,
        AuthenticatorData = p.AuthenticatorData,
        Signature = p.Signature,
    };

    private async Task<string> Fails(WalletProof proof) =>
        (await Assert.ThrowsAsync<WalletAuth.WalletAuthException>(() => WalletAuth.Verify(proof, _hosts))).Message;

    [Fact]
    public async Task Verify_AcceptsAParticipatingAccount()
    {
        Assert.Equal(F.Direct.Address, await WalletAuth.Verify(F.Direct, _hosts));
    }

    [Fact]
    public async Task Verify_AcceptsARekeyedAccountSignedByItsAuthAddress()
    {
        Assert.Equal(F.Rekeyed.Address, await WalletAuth.Verify(Copy(F.Rekeyed), _hosts));
    }

    [Fact]
    public async Task Verify_UsesEachChallengeOnce()
    {
        await WalletAuth.Verify(F.Direct, _hosts);

        Assert.Contains("expired", await Fails(F.Direct));
    }

    [Fact]
    public async Task Verify_RejectsAnExpiredChallenge()
    {
        WalletAuth.AddChallenge(F.Nonce, DateTime.UtcNow.AddSeconds(-1));

        Assert.Contains("expired", await Fails(F.Direct));
    }

    [Fact]
    public async Task Verify_RejectsASignatureForAnotherSite()
    {
        string message = (await Assert.ThrowsAsync<WalletAuth.WalletAuthException>(
            () => WalletAuth.Verify(F.Direct, ["10.0.0.2:3537"]))).Message;

        Assert.Contains("different site", message);
    }

    [Fact]
    public async Task Verify_RejectsATamperedMessage()
    {
        WalletProof proof = Copy(F.Direct);
        string json = Encoding.UTF8.GetString(Convert.FromBase64String(proof.Data)).Replace("Sign in to FUNC", "Sign in to FUNC!");
        proof.Data = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));

        Assert.Contains("not valid", await Fails(proof));
    }

    [Fact]
    public async Task Verify_RejectsAMismatchedAddress()
    {
        WalletProof proof = Copy(F.Direct);
        proof.Address = F.Rekeyed.Address;

        Assert.Contains("doesn't match", await Fails(proof));
    }

    [Fact]
    public async Task Verify_RejectsAnAccountWithoutParticipationKeys()
    {
        _participating.Remove(F.Direct.Address);

        Assert.Contains("participation keys", await Fails(F.Direct));
    }

    [Fact]
    public async Task Verify_RejectsTheOriginalKeyOfARekeyedAccount()
    {
        _authAddresses[F.Direct.Address] = F.Rekeyed.AuthAddr;

        Assert.Contains("rekeyed", await Fails(F.Direct));
    }

    [Fact]
    public async Task Verify_RejectsARekeyedAccountSigningForItself()
    {
        _authAddresses.Remove(F.Rekeyed.Address);

        Assert.Contains("different account", await Fails(Copy(F.Rekeyed)));
    }

    [Fact]
    public async Task Enabled_FollowsParticipationKeys()
    {
        Assert.True(await WalletAuth.Enabled());

        _participating.Clear();
        WalletAuth.ClearCache();

        Assert.False(await WalletAuth.Enabled());
    }

    [Fact]
    public void PublicKey_DecodesAnAddress()
    {
        Assert.Equal(Convert.FromBase64String(F.Direct.Signer), WalletAuth.PublicKey(F.Direct.Address));
    }

    // ---- Through the controller ----

    // A controller for a request with this Host header, on a FUNC configured to also
    // accept the given hosts (Auth:Hosts in func.json).
    private static AuthController Controller(string host, params string[] configuredHosts)
    {
        var http = new DefaultHttpContext();
        http.Request.Host = new HostString(host);
        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(configuredHosts.Select((h, i) => new KeyValuePair<string, string?>($"Auth:Hosts:{i}", h)))
            .Build();
        return new AuthController(NullLogger<AuthController>.Instance, config)
        {
            ControllerContext = new ControllerContext { HttpContext = http },
        };
    }

    [Fact]
    public async Task WalletLogin_IssuesASession()
    {
        Auth.SetPassword(Password);

        ActionResult<Session> result = await Controller(F.Domain, F.Domain).WalletLogin(F.Direct);

        Assert.True(Auth.ValidateSession(result.Value!.Token));
    }

    [Fact]
    public async Task WalletLogin_IgnoresTheHostHeader()
    {
        Auth.SetPassword(Password);

        // A signature relayed from another site, with the Host header set to match it.
        ActionResult<Session> result = await Controller(F.Domain).WalletLogin(F.Direct);

        Assert.IsType<UnauthorizedObjectResult>(result.Result);
    }

    [Fact]
    public void LocalHosts_CoverLocalhostOnFuncsPorts()
    {
        string[] hosts = [.. WalletAuth.LocalHosts()];

        Assert.Contains("localhost:3536", hosts);
        Assert.Contains("localhost:3537", hosts);
        Assert.Contains("127.0.0.1:3537", hosts);
    }

    [Fact]
    public async Task WalletLogin_NeedsAPasswordFirst()
    {
        File.Delete(Auth.AuthPath);

        ActionResult<Session> result = await Controller(F.Domain, F.Domain).WalletLogin(F.Direct);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task ChangePassword_AcceptsAWalletSignatureInsteadOfThePassword()
    {
        Auth.SetPassword(Password);

        ActionResult<Session> result = await Controller(F.Domain, F.Domain).ChangePassword(
            new ChangePassword { Wallet = F.Direct, NewPassword = "a new password" });

        Assert.True(Auth.ValidateSession(result.Value!.Token));
        Assert.True(Auth.VerifyPassword("a new password"));
    }

    [Fact]
    public async Task ChangePassword_NeedsThePasswordOrAWallet()
    {
        Auth.SetPassword(Password);

        ActionResult<Session> result = await Controller(F.Domain, F.Domain).ChangePassword(
            new ChangePassword { NewPassword = "a new password" });

        Assert.IsType<UnauthorizedObjectResult>(result.Result);
        Assert.True(Auth.VerifyPassword(Password));
    }
}
