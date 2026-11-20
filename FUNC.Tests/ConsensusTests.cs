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

        // Untimed first call absorbs JIT/handler setup, which on a loaded CI runner can take
        // most of a second. The ~2s retry this guards against would hit every call, not just the first.
        await Assert.ThrowsAnyAsync<Exception>(() => Diagnostics.AlgodGet(Consensus.statusHttp, port, "/v2/status", "t"));

        var sw = System.Diagnostics.Stopwatch.StartNew();
        await Assert.ThrowsAnyAsync<Exception>(() => Diagnostics.AlgodGet(Consensus.statusHttp, port, "/v2/status", "t"));
        // ConnectTimeout is 500ms; anything near the ~2s retry means it isn't applied.
        Assert.True(sw.ElapsedMilliseconds < 1500, $"took {sw.ElapsedMilliseconds} ms");
    }

    private const string Lacks = "the network's current consensus protocol";

    [Fact]
    public void ReleaseOptions_EndAtNewestUnsupported()
    {
        var options = FUNC.Controllers.GoalController.ReleaseOptions(
            ["v4.1.0-stable", "v4.0.2-stable", "v4.0.1-stable", "v3.27.0-stable"],
            [null, null, Lacks, Lacks]);
        Assert.Equal(["v4.1.0-stable", "v4.0.2-stable", "v4.0.1-stable"], options.Select(o => o.Name));
        Assert.Null(options[1].Unsupported);
        Assert.Equal($"This and earlier versions don't support {Lacks}", options[2].Unsupported);
    }

    // GitHub lists by date; a late hotfix on an old line mustn't cut off newer versions.
    [Fact]
    public void ReleaseOptions_OrderByVersionNotDate()
    {
        var options = FUNC.Controllers.GoalController.ReleaseOptions(
            ["v3.27.1-stable", "v4.1.0-stable", "v4.0.10-stable", "v4.0.9-stable"],
            [Lacks, null, null, Lacks]);
        Assert.Equal(["v4.1.0-stable", "v4.0.10-stable", "v4.0.9-stable"], options.Select(o => o.Name));
    }

    [Fact]
    public void ReleaseOptions_AllSupportedWhenUnknown()
    {
        var options = FUNC.Controllers.GoalController.ReleaseOptions(["v4.1.0-stable", "v4.0.2-stable"], [null, null]);
        Assert.All(options, o => Assert.Null(o.Unsupported));
        Assert.Equal(2, options.Count);
    }
}
