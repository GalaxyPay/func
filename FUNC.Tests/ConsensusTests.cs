using System.Text.Json.Nodes;

namespace FUNC.Tests;

public class ConsensusTests
{
    private const string V42 = "https://github.com/algorandfoundation/specs/tree/953304de35264fc3ef91bcd05c123242015eeaed";
    private const string V43 = "https://github.com/algorandfoundation/specs/tree/73c43b4a9699390884c51e2a9598d49e28373d10";
    private const string V44 = "https://github.com/algorandfoundation/specs/tree/0000000000000000000000000000000000000044";

    // Shaped like go-algorand's protocol/consensus.go.
    private const string Source = $$"""
        // ConsensusV7 increases MaxBalLookback to 320 in preparation for
        const ConsensusV7 = ConsensusVersion("https://github.com/algorand/spec/tree/0000000000000000000000000000000000000007")

        // ConsensusV42 enables state proofs.
        const ConsensusV42 = ConsensusVersion(
        	"{{V42}}",
        )

        const ConsensusV43 = ConsensusVersion(
        	"{{V43}}",
        )

        // ConsensusFuture is a protocol that should not appear in any production
        const ConsensusFuture = ConsensusVersion(
        	"future",
        )
        """;

    [Fact]
    public void ParseProtocols_FindsEveryDeclaredVersion()
    {
        var protocols = Consensus.ParseProtocols(Source);
        Assert.Equal(3, protocols.Count);
        Assert.Contains(V42, protocols);
        Assert.Contains(V43, protocols);
        Assert.Contains("https://github.com/algorand/spec/tree/0000000000000000000000000000000000000007", protocols);
    }

    [Fact]
    public void FromStatus_NoPendingUpgrade()
    {
        var status = JsonNode.Parse($$"""{"last-version":"{{V43}}","next-version":"{{V43}}","next-version-round":101}""") as JsonObject;
        Assert.Equal(new Consensus.Requirement(V43, null, null), Consensus.FromStatus(status));
    }

    [Fact]
    public void FromStatus_PendingUpgrade()
    {
        var status = JsonNode.Parse($$"""{"last-version":"{{V43}}","next-version":"{{V44}}","next-version-round":56000000}""") as JsonObject;
        Assert.Equal(new Consensus.Requirement(V43, V44, 56_000_000UL), Consensus.FromStatus(status));
    }

    [Fact]
    public void FromStatus_NoProtocol() =>
        Assert.Null(Consensus.FromStatus(JsonNode.Parse("{}") as JsonObject));

    [Fact]
    public void Blocker_AllowsReleaseThatKnowsCurrentProtocol() =>
        Assert.Null(Consensus.Blocker(Consensus.ParseProtocols(Source), new(V43, null, null)));

    [Fact]
    public void Blocker_BlocksReleaseMissingCurrentProtocol() =>
        Assert.Contains("current consensus protocol",
            Consensus.Blocker(Consensus.ParseProtocols(Source), new(V44, null, null)));

    [Fact]
    public void Blocker_BlocksReleaseMissingApprovedUpgrade() =>
        Assert.Contains("upgrade at round",
            Consensus.Blocker(Consensus.ParseProtocols(Source), new(V43, V44, 56_000_000)));

    [Fact]
    public void Blocker_AllowsWhenReleaseSourceUnknown() =>
        Assert.Null(Consensus.Blocker((HashSet<string>?)null, new(V44, null, null)));

    [Fact]
    public void InstalledBlocker_UnsupportedUpgrade() =>
        Assert.Contains("upgrade at round",
            Consensus.InstalledBlocker(JsonNode.Parse("""{"next-version-supported":false,"next-version-round":56000000}""") as JsonObject));

    [Fact]
    public void InstalledBlocker_SupportedUpgrade() =>
        Assert.Null(Consensus.InstalledBlocker(JsonNode.Parse("""{"next-version-supported":true,"next-version-round":56000000}""") as JsonObject));

    [Fact]
    public void InstalledBlocker_NodeUnreachable() =>
        Assert.Null(Consensus.InstalledBlocker(null));

    // Windows takes ~2s to fail a connect to a closed localhost port; a stopped node
    // must not hold up goal/version that long.
    [Fact]
    public async Task StatusClient_FailsFastWhenNodeStopped()
    {
        var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        int port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();

        var sw = System.Diagnostics.Stopwatch.StartNew();
        await Assert.ThrowsAnyAsync<Exception>(() => Diagnostics.AlgodGet(Consensus.statusHttp, port, "/v2/status", "t"));
        Assert.True(sw.ElapsedMilliseconds < 500, $"took {sw.ElapsedMilliseconds} ms");
    }
}
