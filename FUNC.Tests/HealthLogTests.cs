using System.Text;
using FUNC.Health;

namespace FUNC.Tests;

// The fixture is a slice of a real mainnet node.log (algod 5.0.5), with the node's
// own addresses and session ids replaced: 49 rounds with their telemetry events, two
// heartbeats, a peer list, all warnings, and 20 lines of the usual noise.
internal static class Fixture
{
    public static readonly string NodeLog = Path.Combine(AppContext.BaseDirectory, "Fixtures", "node-log.jsonl");

    public static byte[][] Lines() =>
        [.. File.ReadAllLines(NodeLog).Where(l => l.Length > 0).Select(Encoding.UTF8.GetBytes)];

    public static List<HealthEvent> Events() =>
        [.. Lines().Select(l => LogParser.Parse(l)).OfType<HealthEvent>()];
}

public class LogParserTests
{
    private static HealthEvent? Parse(string line) => LogParser.Parse(Encoding.UTF8.GetBytes(line));

    [Fact]
    public void Parse_RecognizesEveryEventInARealLog()
    {
        var counts = Fixture.Events().GroupBy(e => e.Kind).ToDictionary(g => g.Key, g => g.Count());

        Assert.Equal(new Dictionary<HealthEventKind, int>
        {
            [HealthEventKind.RoundConcluded] = 49,
            [HealthEventKind.BlockAccepted] = 49,
            [HealthEventKind.VoteSent] = 57,
            [HealthEventKind.BlockProposed] = 2,
            [HealthEventKind.ConnectPeer] = 1,
            [HealthEventKind.DisconnectPeer] = 1,
            [HealthEventKind.PeerConnections] = 1,
            [HealthEventKind.Heartbeat] = 2,
            [HealthEventKind.Warning] = 12,
        }, counts);
    }

    [Fact]
    public void IsCandidate_SkipsTheNoise()
    {
        byte[][] lines = Fixture.Lines();

        // Received votes and transaction pool chatter make up nearly all of a real log.
        Assert.Equal(20, lines.Count(l => !LogParser.IsCandidate(l)));
        Assert.Equal(lines.Length - 20, Fixture.Events().Count);
    }

    [Fact]
    public void Parse_ReadsAgreementEvents()
    {
        List<HealthEvent> events = Fixture.Events();

        HealthEvent round = events.First(e => e.Kind == HealthEventKind.RoundConcluded);
        Assert.Equal(66865574, round.Round);
        Assert.Equal(0, round.Period);
        Assert.Equal(DateTimeOffset.Parse("2026-11-11T14:58:46.712361-07:00"), round.Time);

        HealthEvent block = events.First(e => e.Kind == HealthEventKind.BlockAccepted);
        Assert.Equal(66865574, block.Round);
        Assert.Equal(350.674291, block.ReceivedMs, 6);
        Assert.Equal("5.0.5", block.Version);

        Assert.Equal(66865575, events.First(e => e.Kind == HealthEventKind.VoteSent).Round);
        Assert.Equal(66865713, events.First(e => e.Kind == HealthEventKind.BlockProposed).Round);
    }

    [Fact]
    public void Parse_ReadsPeerEvents()
    {
        List<HealthEvent> events = Fixture.Events();

        Assert.Equal("r-sb.algorand-mainnet.network:4160", events.Single(e => e.Kind == HealthEventKind.ConnectPeer).Text);
        Assert.Equal("r-x1.algorand-mainnet.network:4160 (LeastPerformingPeer)", events.Single(e => e.Kind == HealthEventKind.DisconnectPeer).Text);

        List<PeerInfo> peers = events.Single(e => e.Kind == HealthEventKind.PeerConnections).Peers!;
        Assert.Equal(3, peers.Count);
        Assert.All(peers, p => Assert.False(p.Incoming));
        Assert.Equal("r-is.algorand-mainnet.network:4160", peers[0].Endpoint);
        Assert.Equal("mr-dal-6b6c.r2.algonode.net", peers[0].Host);
        Assert.Equal(7919, peers[0].ConnectedSeconds);
    }

    [Fact]
    public void Parse_ReadsHeartbeatMetrics()
    {
        HealthEvent heartbeat = Fixture.Events().First(e => e.Kind == HealthEventKind.Heartbeat);

        Assert.Equal(4, heartbeat.Metrics!["algod_network_outgoing_peers"]);
        Assert.Equal(0, heartbeat.Metrics["algod_network_incoming_peers"]);
        Assert.Equal(5707237983, heartbeat.Metrics["algod_network_received_bytes_total"]);
    }

    [Fact]
    public void Parse_ReadsWarnings()
    {
        HealthEvent warning = Fixture.Events().First(e => e.Kind == HealthEventKind.Warning);

        Assert.Equal("AssembleBlock: assembled block round did not catch up to requested round: 66865793 != 66865794", warning.Text);
    }

    [Theory]
    [InlineData("error")]
    [InlineData("fatal")]
    [InlineData("panic")]
    public void Parse_TreatsSevereLevelsAsErrors(string level)
    {
        HealthEvent? ev = Parse($$"""{"file":"x.go","level":"{{level}}","msg":"disk full","time":"2026-11-11T15:00:00.000000Z"}""");

        Assert.Equal(HealthEventKind.Error, ev?.Kind);
        Assert.Equal("disk full", ev?.Text);
    }

    [Fact]
    public void Parse_ClipsLongMessages()
    {
        string msg = new('x', 5000);

        HealthEvent? ev = Parse($$"""{"level":"warning","msg":"{{msg}}","time":"2026-11-11T15:00:00.000000Z"}""");

        Assert.Equal(301, ev?.Text?.Length);
    }

    [Fact]
    public void Parse_AcceptsFieldsAtTheTopLevel()
    {
        // The layout without a "details" object.
        HealthEvent? ev = Parse("""{"Round":7,"ReceivedAt":2500000,"level":"info","msg":"/Agreement/BlockAccepted","time":"2026-11-11T15:00:00.000000Z"}""");

        Assert.Equal(HealthEventKind.BlockAccepted, ev?.Kind);
        Assert.Equal(7, ev?.Round);
        Assert.Equal(2.5, ev?.ReceivedMs);
    }

    [Fact]
    public void Parse_ReadsStartupAndCatchup()
    {
        HealthEvent? startup = Parse("""{"details":{"Version":"5.0.5","CommitHash":"31186e99"},"level":"info","msg":"/ApplicationState/Startup","time":"2026-11-11T15:00:00.000000Z"}""");
        HealthEvent? start = Parse("""{"details":{"StartRound":100},"level":"info","msg":"/ApplicationState/CatchupStart","time":"2026-11-11T15:00:00.000000Z"}""");
        HealthEvent? stop = Parse("""{"details":{"StartRound":100,"EndRound":250},"level":"info","msg":"/ApplicationState/CatchupStop","time":"2026-11-11T15:00:00.000000Z"}""");

        Assert.Equal((HealthEventKind.Startup, "5.0.5"), (startup!.Kind, startup.Version));
        Assert.Equal((HealthEventKind.CatchupStart, 100), (start!.Kind, start.Round));
        Assert.Equal((HealthEventKind.CatchupStop, 250), (stop!.Kind, stop.Round));
    }

    [Theory]
    // An event the dashboard doesn't use.
    [InlineData("""{"details":{},"level":"info","msg":"/ApplicationState/Shutdown","time":"2026-11-11T15:00:00.000000Z"}""")]
    // A line cut off mid-write.
    [InlineData("""{"details":{"Round":7},"level":"info","msg":"/Agreement/Blo""")]
    // No usable time.
    [InlineData("""{"level":"warning","msg":"x"}""")]
    // Quoted text inside a message is escaped, so it can't pose as a field.
    [InlineData("""{"level":"info","msg":"saw \"level\":\"warning\" in a peer's header","time":"2026-11-11T15:00:00.000000Z"}""")]
    [InlineData("""["level","warning"]""")]
    [InlineData("\"level\":\"warning\" but not json")]
    public void Parse_IgnoresEverythingElse(string line)
    {
        Assert.Null(Parse(line));
    }
}

public class LogTailerTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"func-tail-{Guid.NewGuid():N}.txt");
    private readonly LogTailer _tailer = new();
    private readonly List<string> _lines = [];

    public void Dispose() => File.Delete(_path);

    // A line long enough to identify the file, as real log lines are.
    private static string Line(string id) => $"{{\"id\":\"{id}\",\"pad\":\"{new string('.', 300)}\"}}";

    private long Read(long maxBytes = long.MaxValue) =>
        _tailer.Read(_path, line => _lines.Add(Encoding.UTF8.GetString(line)), maxBytes);

    [Fact]
    public void Read_ReturnsOnlyWhatIsNew()
    {
        File.WriteAllText(_path, Line("a") + "\n" + Line("b") + "\n");
        Assert.Equal(new FileInfo(_path).Length, Read());
        Assert.Equal([Line("a"), Line("b")], _lines);

        Assert.Equal(0, Read());

        File.AppendAllText(_path, Line("c") + "\n");
        Read();
        Assert.Equal([Line("a"), Line("b"), Line("c")], _lines);
        Assert.Equal(new FileInfo(_path).Length, _tailer.Offset);
    }

    [Fact]
    public void Read_WaitsForALineToBeFinished()
    {
        string half = Line("b")[..100];
        File.WriteAllText(_path, Line("a") + "\n" + half);
        Read();
        Assert.Equal([Line("a")], _lines);

        File.AppendAllText(_path, Line("b")[100..] + "\n");
        Read();
        Assert.Equal([Line("a"), Line("b")], _lines);
    }

    [Fact]
    public void Read_HandlesWindowsLineEndingsAndBlankLines()
    {
        File.WriteAllText(_path, Line("a") + "\r\n\r\n" + Line("b") + "\r\n");

        Read();

        Assert.Equal([Line("a"), Line("b")], _lines);
    }

    [Fact]
    public void Read_StartsOverWhenTheLogIsRotated()
    {
        File.WriteAllText(_path, Line("a") + "\n" + Line("b") + "\n");
        Read();

        // algod renames the full log away and starts a new one, which may already
        // be longer than the old one by the time anyone looks.
        File.WriteAllText(_path, Line("c") + "\n" + Line("d") + "\n" + Line("e") + "\n");
        Read();

        Assert.Equal([Line("a"), Line("b"), Line("c"), Line("d"), Line("e")], _lines);
    }

    [Fact]
    public void Read_ResumesFromASavedPosition()
    {
        File.WriteAllText(_path, Line("a") + "\n" + Line("b") + "\n");
        Read();
        LogTailer resumed = new() { Offset = _tailer.Offset, Fingerprint = _tailer.Fingerprint };
        File.AppendAllText(_path, Line("c") + "\n");
        List<string> lines = [];

        resumed.Read(_path, line => lines.Add(Encoding.UTF8.GetString(line)), long.MaxValue);

        Assert.Equal([Line("c")], lines);
    }

    [Fact]
    public void Read_StopsAtTheByteLimitAndContinuesLater()
    {
        File.WriteAllLines(_path, Enumerable.Range(0, 50).Select(i => Line($"{i}")));

        long first = Read(maxBytes: 1);
        Assert.True(first > 0);
        while (Read(maxBytes: 1) > 0) { }

        Assert.Equal(Enumerable.Range(0, 50).Select(i => Line($"{i}")), _lines);
    }

    [Fact]
    public void Read_DeliversLinesLongerThanItsBuffer()
    {
        string big = "{\"peers\":\"" + new string('p', 3 * 1024 * 1024) + "\"}";
        File.WriteAllText(_path, Line("a") + "\n" + big + "\n" + Line("b") + "\n");

        Read();

        Assert.Equal([Line("a"), big, Line("b")], _lines);
    }

    [Fact]
    public void Read_WaitsForTheFirstLine()
    {
        File.WriteAllText(_path, "{\"start");

        Assert.Equal(0, Read());
        Assert.Null(_tailer.Fingerprint);
    }

    [Fact]
    public void Read_WorksWhileTheNodeHoldsTheFileOpen()
    {
        using FileStream node = new(_path, FileMode.Create, FileAccess.Write, FileShare.Read | FileShare.Delete);
        node.Write(Encoding.UTF8.GetBytes(Line("a") + "\n"));
        node.Flush();

        Read();

        Assert.Equal([Line("a")], _lines);
    }
}

public class PrometheusTests
{
    [Fact]
    public void Parse_ReadsNamesAndValues()
    {
        var metrics = Prometheus.Parse("""
            # HELP algod_ledger_round the last round
            # TYPE algod_ledger_round gauge
            algod_ledger_round 66865648
            algod_network_outgoing_peers{host="a b", session="x"} 4
            algod_network_received_bytes_total 5.707237983e+09

            algod_tagged{tag="TX"} 3
            algod_tagged{tag="VP"} 4
            algod_with_timestamp 7 1700000000000
            not a metric line
            algod_not_a_number NaN
            """);

        Assert.Equal(66865648, metrics["algod_ledger_round"]);
        Assert.Equal(4, metrics["algod_network_outgoing_peers"]);
        Assert.Equal(5707237983, metrics["algod_network_received_bytes_total"]);
        Assert.Equal(7, metrics["algod_tagged"]);
        Assert.Equal(7, metrics["algod_with_timestamp"]);
        Assert.Equal(5, metrics.Count);
    }

    [Fact]
    public void CounterDeltas_ReportsWhatWasAdded()
    {
        CounterDeltas deltas = new();

        Assert.Equal(0, deltas.Next("bytes", 1000));
        Assert.Equal(250, deltas.Next("bytes", 1250));
        Assert.Equal(0, deltas.Next("bytes", 1250));
        // The node restarted and the counter began again.
        Assert.Equal(40, deltas.Next("bytes", 40));
        Assert.Equal(0, deltas.Next("other", 5));
    }
}
