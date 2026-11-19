using FUNC.Controllers;

namespace FUNC.Tests;

// Fixtures are real go-algorand v5.0.5 release files, signed by Algorand's 2022 subkey
// (F321C257…, expired 2026-10-31), plus releases.algorand.com/key.pub as of 2026-11, which
// only carries the 2026 subkey (A2696A69…).
public class PgpTests
{
    private static readonly string Dir = Path.Combine(AppContext.BaseDirectory, "Fixtures", "pgp");
    private const string HashesName = "hashes_stable_linux_amd64_5.0.5";
    private const string OldSubkey = "F321C257B7CC77F631EE89C3007CB592A5D131FE";
    private const string NewSubkey = "A2696A69562A3F158F5DD446F1C9063D88DB5791";

    private static byte[] Hashes => File.ReadAllBytes(Path.Combine(Dir, HashesName));
    private static byte[] Sig => File.ReadAllBytes(Path.Combine(Dir, HashesName + ".sig"));
    private static string Embedded => Pgp.ReadEmbedded("algorand-release.asc");
    private static string LiveKey => File.ReadAllText(Path.Combine(Dir, "key-2026-11.pub"));

    private static List<Pgp.SigningKey> Keys(params string[] keyrings) =>
        Pgp.LoadSigningKeys(GoalController.AlgorandPrimaryFingerprint, keyrings);

    [Fact]
    public void EmbeddedKeyring_HasBothCertifiedSubkeys()
    {
        var keys = Keys(Embedded);
        Assert.Equal([NewSubkey, OldSubkey], keys.Select(k => k.FingerprintHex).Order());

        var old = keys.Single(k => k.FingerprintHex == OldSubkey);
        Assert.Equal(new DateTime(2026, 10, 31), old.Expires!.Value.UtcDateTime.Date);
    }

    [Fact]
    public void LiveAndEmbeddedKeyrings_MergeWithoutDuplicates()
    {
        Assert.Equal(2, Keys(Embedded, LiveKey).Count);
    }

    [Fact]
    public void MalformedKeyrings_AreSkipped()
    {
        Assert.Equal(2, Keys(Embedded, "", "<html>not a key</html>",
            "-----BEGIN PGP PUBLIC KEY BLOCK-----\n\n!!!!\n-----END PGP PUBLIC KEY BLOCK-----").Count);
    }

    [Fact]
    public void AnotherPrimaryFingerprint_TrustsNothing()
    {
        Assert.Empty(Pgp.LoadSigningKeys(new string('0', 40), Embedded));
    }

    [Fact]
    public void Verify_AcceptsTheReleaseSignature()
    {
        Assert.True(Pgp.Verify(Hashes, Sig, Keys(Embedded)));
    }

    [Fact]
    public void Verify_RejectsTamperedData()
    {
        byte[] data = Hashes;
        data[0] ^= 1;
        Assert.False(Pgp.Verify(data, Sig, Keys(Embedded)));
    }

    [Fact]
    public void Verify_RejectsATamperedSignature()
    {
        byte[] sig = Sig;
        sig[^1] ^= 1;
        Assert.False(Pgp.Verify(Hashes, sig, Keys(Embedded)));
    }

    [Fact]
    public void Verify_NeedsTheSubkeyThatSigned()
    {
        // The current key.pub dropped the expired subkey, so it alone can't verify v5.0.5.
        Assert.False(Pgp.Verify(Hashes, Sig, Keys(LiveKey)));
    }

    [Fact]
    public void Verify_RejectsSignaturesMadeOutsideTheKeyLifetime()
    {
        var keys = Keys(Embedded).Select(k => k with { Expires = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero) }).ToList();
        Assert.False(Pgp.Verify(Hashes, Sig, keys));
    }

    [Fact]
    public void Verify_RejectsGarbage()
    {
        Assert.False(Pgp.Verify(Hashes, [], Keys(Embedded)));
        Assert.False(Pgp.Verify(Hashes, [0xFF, 0x00, 0x01], Keys(Embedded)));
        Assert.False(Pgp.Verify(Hashes, Sig[..100], Keys(Embedded)));
    }

    [Fact]
    public void FindSha256_PicksTheSha256Line()
    {
        string listing = System.Text.Encoding.UTF8.GetString(Hashes);
        Assert.Equal("312ce90bc4b1f5ded20b005ea8173209c2740f4ea84cfe14f79c050a874bc8b1",
            GoalController.FindSha256(listing, "node_stable_linux-amd64_5.0.5.tar.gz"));
        Assert.Null(GoalController.FindSha256(listing, "node_stable_linux-amd64_5.0.4.tar.gz"));
    }
}
