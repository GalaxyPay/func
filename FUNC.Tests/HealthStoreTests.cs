using FUNC.Health;
using FUNC.Models;

namespace FUNC.Tests;

public class HealthStoreTests : IDisposable
{
    // Shortly after the fixture's last event (23:13 UTC).
    private DateTimeOffset _now = DateTimeOffset.Parse("2026-11-11T23:30:00Z");
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"func-health-{Guid.NewGuid():N}.json");

    public void Dispose()
    {
        File.Delete(_path);
        File.Delete(_path + ".tmp");
    }

    private HealthStore NewStore(bool persisted = false) => new(persisted ? _path : null, () => _now);

    private HealthStore FixtureStore(bool persisted = false)
    {
        HealthStore store = NewStore(persisted);
        foreach (HealthEvent ev in Fixture.Events()) store.Apply(ev);
        return store;
    }

    private static HealthEvent Round(long round, DateTimeOffset time, int period = 0) =>
        new() { Kind = HealthEventKind.RoundConcluded, Round = round, Time = time, Period = period };

    private static HealthEvent Vote(DateTimeOffset time) => new() { Kind = HealthEventKind.VoteSent, Time = time };

    private static HealthEvent Warning(DateTimeOffset time, string text) =>
        new() { Kind = HealthEventKind.Warning, Time = time, Text = text };

    [Fact]
    public void Query_TotalsARealLog()
    {
        NodeHealth health = FixtureStore().Query("24h");
        List<HealthPoint> points = health.Points;

        Assert.Equal(49, points.Sum(p => p.Rounds));
        Assert.Equal(49, points.Sum(p => p.Blocks));
        Assert.Equal(57, points.Sum(p => p.Votes));
        Assert.Equal(2, points.Sum(p => p.Proposals));
        // Both went to other proposers' blocks.
        Assert.Equal(0, points.Sum(p => p.Wins));
        Assert.Equal(12, points.Sum(p => p.Warnings));
        Assert.Equal(0, points.Sum(p => p.Errors));
        Assert.Equal(1, points.Sum(p => p.Connects));
        Assert.Equal(1, points.Sum(p => p.Disconnects));
        Assert.Equal(0, points.Sum(p => p.Recoveries));

        Assert.Equal(66865622, health.LastRound);
        Assert.Equal(DateTimeOffset.Parse("2026-11-11T15:00:59.335076-07:00"), health.LastRoundAt);
        Assert.Equal("5.0.5", health.Version);
    }

    [Fact]
    public void Query_AveragesRoundAndArrivalTimes()
    {
        // The 49 rounds straddle an hour boundary; a six-hour point holds them all.
        HealthPoint point = FixtureStore().Query("30d").Points.Single(p => p.Rounds > 0);

        // 48 gaps between 49 consecutive rounds, 132.622715 s in all.
        Assert.Equal(132622.715 / 48, point.RoundMs!.Value, 3);
        Assert.Equal(2934.561, point.RoundMsMax, 3);
        Assert.Equal(5492.54705 / 49, point.ArrivalMs!.Value, 3);
        Assert.Equal(888.154792, point.ArrivalMsMax, 3);
    }

    [Fact]
    public void Query_ReportsPeersFromTheLog()
    {
        NodeHealth health = FixtureStore().Query("24h");

        // Counts from the heartbeats, the list from the hourly peer report.
        Assert.Equal(0, health.PeersIn);
        Assert.Equal(4, health.PeersOut);
        Assert.Equal(3, health.Peers.Count);
        Assert.Equal(DateTimeOffset.Parse("2026-11-11T15:25:14.552919-07:00"), health.PeersAt);
        Assert.Contains(health.Points, p => p.PeersOut == 4);
        Assert.False(health.Metrics);
    }

    [Fact]
    public void Query_ListsRecentEventsNewestFirst()
    {
        List<HealthNote> notes = FixtureStore().Query("24h").Notes;

        // The 12 warnings; the routine swap of the slowest relay is not listed.
        Assert.Equal(12, notes.Count);
        Assert.All(notes, n => Assert.Equal("warning", n.Level));
        Assert.StartsWith("AssembleBlock: assembled block round did not catch up", notes[0].Text);
        Assert.Equal(DateTimeOffset.Parse("2026-11-11T16:13:40.629373-07:00"), notes[0].Time);
    }

    [Fact]
    public void Apply_ListsUnexpectedDisconnects()
    {
        HealthStore store = NewStore();

        store.Apply(new HealthEvent { Kind = HealthEventKind.DisconnectPeer, Time = _now.AddMinutes(-2), Text = "r-x1.example:4160 (LeastPerformingPeer)" });
        store.Apply(new HealthEvent { Kind = HealthEventKind.DisconnectPeer, Time = _now.AddMinutes(-1), Text = "r-x2.example:4160 (ReadError)" });

        NodeHealth health = store.Query("1h");
        Assert.Equal(2, health.Points.Sum(p => p.Disconnects));
        Assert.Equal("Peer disconnected: r-x2.example:4160 (ReadError)", Assert.Single(health.Notes).Text);
    }

    [Theory]
    [InlineData("1h", 60, 60)]
    [InlineData("24h", 900, 96)]
    [InlineData(null, 900, 96)]
    [InlineData("7d", 3600, 168)]
    [InlineData("30d", 21600, 120)]
    public void Query_ReturnsEvenlySpacedPointsUpToNow(string? range, int width, int count)
    {
        NodeHealth health = FixtureStore().Query(range);

        Assert.Equal(width, health.BucketSeconds);
        Assert.Equal(count, health.Points.Count);
        long last = health.Points[^1].T;
        Assert.InRange(_now.ToUnixTimeSeconds(), last, last + width - 1);
        Assert.All(health.Points.Zip(health.Points.Skip(1)), pair => Assert.Equal(width, pair.Second.T - pair.First.T));
    }

    [Fact]
    public void Query_CoversOnlyTheRequestedRange()
    {
        HealthStore store = FixtureStore();

        // The last hour starts at 22:31 UTC: after the rounds, with 8 of the warnings.
        Assert.Equal(0, store.Query("1h").Points.Sum(p => p.Rounds));
        Assert.Equal(8, store.Query("1h").Points.Sum(p => p.Warnings));
        Assert.Equal(49, store.Query("7d").Points.Sum(p => p.Rounds));
        Assert.Equal(49, store.Query("30d").Points.Sum(p => p.Rounds));
    }

    [Fact]
    public void Query_RejectsUnknownRanges()
    {
        Assert.Equal("Range must be 1h, 24h, 7d or 30d", Assert.Throws<Exception>(() => NewStore().Query("1y")).Message);
    }

    [Fact]
    public void Apply_KeepsOlderHistoryByTheHourOnly()
    {
        HealthStore store = NewStore();

        store.Apply(Vote(_now.AddDays(-3)));
        store.Apply(Vote(_now.AddDays(-31)));
        store.Apply(Vote(_now.AddHours(2)));

        Assert.Equal(0, store.Query("24h").Points.Sum(p => p.Votes));
        Assert.Equal(1, store.Query("7d").Points.Sum(p => p.Votes));
        Assert.Equal(1, store.Query("30d").Points.Sum(p => p.Votes));
    }

    private static HealthEvent Proposed(long round, string hash, DateTimeOffset time, int period = 0) =>
        new() { Kind = HealthEventKind.BlockProposed, Round = round, Hash = hash, Time = time, Period = period };

    private static HealthEvent Accepted(long round, string hash, DateTimeOffset time) =>
        new() { Kind = HealthEventKind.BlockAccepted, Round = round, Hash = hash, Time = time };

    [Fact]
    public void Apply_CountsAWinWhenTheNetworkTakesTheProposedBlock()
    {
        HealthStore store = NewStore();
        DateTimeOffset t = _now.AddMinutes(-10);

        store.Apply(Proposed(100, "OURS", t));
        store.Apply(Accepted(100, "OURS", t.AddSeconds(2)));
        store.Apply(Proposed(101, "OURS2", t.AddSeconds(3)));
        store.Apply(Accepted(101, "THEIRS", t.AddSeconds(5)));
        // Proposed again in a later period of the same round; the second one won.
        store.Apply(Proposed(102, "FIRST", t.AddSeconds(6)));
        store.Apply(Proposed(102, "SECOND", t.AddSeconds(12), period: 1));
        store.Apply(Accepted(102, "SECOND", t.AddSeconds(14)));

        List<HealthPoint> points = store.Query("1h").Points;
        Assert.Equal(4, points.Sum(p => p.Proposals));
        Assert.Equal(2, points.Sum(p => p.Wins));
    }

    [Fact]
    public void Apply_CountsAWinOnlyOnce()
    {
        HealthStore store = NewStore();
        DateTimeOffset t = _now.AddMinutes(-10);

        store.Apply(Proposed(100, "OURS", t));
        store.Apply(Accepted(100, "OURS", t.AddSeconds(2)));
        store.Apply(Accepted(100, "OURS", t.AddSeconds(2)));

        Assert.Equal(1, store.Query("1h").Points.Sum(p => p.Wins));
    }

    [Fact]
    public void Apply_ForgetsProposalsWhoseRoundNeverShowedUp()
    {
        HealthStore store = NewStore();
        DateTimeOffset t = _now.AddMinutes(-10);

        // The node went down before round 100 was decided.
        store.Apply(Proposed(100, "OURS", t));
        store.Apply(Proposed(500, "LATER", t.AddMinutes(1)));
        store.Apply(Accepted(100, "OURS", t.AddMinutes(2)));

        Assert.Equal(0, store.Query("1h").Points.Sum(p => p.Wins));
    }

    [Fact]
    public void Save_KeepsAProposalThatIsStillUndecided()
    {
        HealthStore store = NewStore(persisted: true);
        DateTimeOffset t = _now.AddMinutes(-10);
        store.Apply(Proposed(100, "OURS", t));
        store.Save();

        // FUNC restarted between the proposal and the round's outcome.
        HealthStore restored = NewStore(persisted: true);
        restored.Apply(Accepted(100, "OURS", t.AddSeconds(2)));

        Assert.Equal(1, restored.Query("1h").Points.Sum(p => p.Wins));
    }

    [Fact]
    public void Apply_CountsARoundOnce()
    {
        HealthStore store = FixtureStore();

        foreach (HealthEvent ev in Fixture.Events().Where(e => e.Kind == HealthEventKind.RoundConcluded)) store.Apply(ev);

        Assert.Equal(49, store.Query("24h").Points.Sum(p => p.Rounds));
    }

    [Fact]
    public void Apply_MeasuresOnlyGapsBetweenConsecutiveRounds()
    {
        HealthStore store = NewStore();
        DateTimeOffset t = _now.AddMinutes(-10);

        store.Apply(Round(100, t));
        store.Apply(Round(101, t.AddSeconds(3)));
        // Rounds 102-199 were not seen (FUNC was off, or the node was catching up).
        store.Apply(Round(200, t.AddSeconds(20)));
        store.Apply(Round(201, t.AddSeconds(24), period: 1));

        List<HealthPoint> points = store.Query("1h").Points;
        Assert.Equal(4, points.Sum(p => p.Rounds));
        Assert.Equal(1, points.Sum(p => p.Recoveries));
        HealthPoint point = points.Single(p => p.Rounds > 0);
        Assert.Equal(3500, point.RoundMs);
        Assert.Equal(4000, point.RoundMsMax);
    }

    [Fact]
    public void Apply_CollapsesARepeatedMessage()
    {
        HealthStore store = NewStore();
        DateTimeOffset t = _now.AddMinutes(-10);

        for (int i = 0; i < 500; i++) store.Apply(Warning(t.AddSeconds(i), "disk is slow"));
        store.Apply(Warning(t.AddSeconds(600), "something else"));

        NodeHealth health = store.Query("1h");
        Assert.Equal(501, health.Points.Sum(p => p.Warnings));
        Assert.Equal(2, health.Notes.Count);
        Assert.Equal(("disk is slow", 500, t.AddSeconds(499)), (health.Notes[1].Text, health.Notes[1].Count, health.Notes[1].Time));
    }

    [Fact]
    public void Apply_KeepsOnlyTheLatestNotes()
    {
        HealthStore store = NewStore();

        for (int i = 0; i < 250; i++) store.Apply(Warning(_now.AddMinutes(-30).AddSeconds(i), $"warning {i}"));

        List<HealthNote> notes = store.Query("1h").Notes;
        Assert.Equal(200, notes.Count);
        Assert.Equal("warning 249", notes[0].Text);
        Assert.Equal("warning 50", notes[^1].Text);
    }

    private static Dictionary<string, double> Metrics(double peersIn, double peersOut, double received, double sent, double droppedVotes = 0) => new()
    {
        ["algod_network_incoming_peers"] = peersIn,
        ["algod_network_outgoing_peers"] = peersOut,
        ["algod_network_received_bytes_total"] = received,
        ["algod_network_sent_bytes_total"] = sent,
        ["algod_agreement_dropped_vote"] = droppedVotes,
    };

    [Fact]
    public void ApplyMetrics_ChartsTrafficBetweenLiveSamples()
    {
        HealthStore store = NewStore();

        store.ApplyMetrics(_now.AddSeconds(-45), Metrics(1, 4, 1_000_000, 50_000), live: true);
        store.ApplyMetrics(_now.AddSeconds(-30), Metrics(1, 4, 1_300_000, 60_000, droppedVotes: 2), live: true);
        store.ApplyMetrics(_now.AddSeconds(-15), Metrics(2, 5, 1_350_000, 61_000, droppedVotes: 2), live: true);

        NodeHealth health = store.Query("1h");
        Assert.True(health.Metrics);
        Assert.Equal((2, 5), (health.PeersIn, health.PeersOut));
        Assert.Equal(350_000, health.Points.Sum(p => p.BytesIn));
        Assert.Equal(11_000, health.Points.Sum(p => p.BytesOut));
        Assert.Equal(2, health.Points.Sum(p => p.Dropped));
        Assert.Equal(5, health.Points.Last(p => p.PeersOut != null).PeersOut);
    }

    [Fact]
    public void ApplyMetrics_TreatsACounterResetAsARestart()
    {
        HealthStore store = NewStore();

        store.ApplyMetrics(_now.AddSeconds(-30), Metrics(0, 4, 9_000_000, 0), live: true);
        store.ApplyMetrics(_now.AddSeconds(-15), Metrics(0, 4, 20_000, 0), live: true);

        Assert.Equal(20_000, store.Query("1h").Points.Sum(p => p.BytesIn));
    }

    [Fact]
    public void ApplyMetrics_UsesHeartbeatsOnlyBeforeLiveSamples()
    {
        HealthStore store = NewStore();

        store.ApplyMetrics(_now.AddMinutes(-20), Metrics(0, 3, 1_000, 1_000), live: false);
        store.ApplyMetrics(_now.AddMinutes(-2), Metrics(0, 4, 5_000, 5_000), live: true);
        store.ApplyMetrics(_now.AddMinutes(-1), Metrics(0, 9, 9_000, 9_000), live: false);

        NodeHealth health = store.Query("1h");
        Assert.Equal(4, health.PeersOut);
        Assert.Equal([3, 4], health.Points.Where(p => p.PeersOut != null).Select(p => p.PeersOut!.Value));
        // Heartbeats are ten minutes apart, too coarse to chart traffic from.
        Assert.Equal(0, health.Points.Sum(p => p.BytesIn));
    }

    [Fact]
    public void ApplyMetrics_GoesStaleWhenSamplesStop()
    {
        HealthStore store = NewStore();
        store.ApplyMetrics(_now, Metrics(0, 4, 0, 0), live: true);

        _now = _now.AddSeconds(90);

        Assert.False(store.Query("1h").Metrics);
    }

    [Fact]
    public void Source_FollowsTheLog()
    {
        HealthStore store = NewStore();
        Assert.Equal("noLog", store.Query("1h").Source);

        store.SetLog(false);
        Assert.Equal("noLog", store.Query("1h").Source);

        // Found, but last written long ago: the node isn't running.
        store.SetLog(true, 100, "f", lastWrite: _now.AddHours(-2));
        Assert.Equal("stale", store.Query("1h").Source);

        store.SetLog(true, 200, "f", grew: true);
        store.Apply(Vote(_now));
        Assert.Equal("ok", store.Query("1h").Source);

        _now = _now.AddSeconds(61);
        store.SetLog(true, 200, "f");
        Assert.Equal("stale", store.Query("1h").Source);
        Assert.Equal(200, store.LogOffset);
    }

    [Fact]
    public void Source_NoticesALogWithoutTelemetry()
    {
        HealthStore store = NewStore();

        // The log keeps growing, but only rounds and warnings come out of it.
        for (int i = 0; i < 7; i++)
        {
            store.SetLog(true, 1000 * (i + 1), "f", grew: true);
            store.Apply(Round(100 + i, _now));
            Assert.Equal(i < 6 ? "ok" : "noEvents", store.Query("1h").Source);
            _now = _now.AddSeconds(55);
        }

        store.SetLog(true, 9000, "f", grew: true);
        store.Apply(Vote(_now));
        Assert.Equal("ok", store.Query("1h").Source);
    }

    [Fact]
    public void Save_RestoresHistoryAndLogPosition()
    {
        HealthStore store = FixtureStore(persisted: true);
        store.SetLog(true, 123456, "fingerprint", grew: true);
        store.Save();

        HealthStore restored = NewStore(persisted: true);

        Assert.Equal(123456, restored.LogOffset);
        Assert.Equal("fingerprint", restored.LogFingerprint);
        NodeHealth before = store.Query("24h"), after = restored.Query("24h");
        Assert.Equivalent(before.Points, after.Points, strict: true);
        Assert.Equivalent(before.Notes, after.Notes, strict: true);
        Assert.Equivalent(before.Peers, after.Peers, strict: true);
        Assert.Equal((before.Version, before.LastRound, before.LastRoundAt), (after.Version, after.LastRound, after.LastRoundAt));
        Assert.Equal(49, restored.Query("7d").Points.Sum(p => p.Rounds));
        // What the sources are doing right now is not history.
        Assert.Equal("noLog", after.Source);
    }

    [Fact]
    public void Save_DropsExpiredHistory()
    {
        HealthStore store = FixtureStore(persisted: true);
        _now = _now.AddDays(2);
        store.Save();

        HealthStore restored = NewStore(persisted: true);

        Assert.Equal(0, restored.Query("24h").Points.Sum(p => p.Rounds));
        Assert.Equal(49, restored.Query("7d").Points.Sum(p => p.Rounds));

        _now = _now.AddDays(31);
        store.Save();
        Assert.Equal(0, NewStore(persisted: true).Query("30d").Points.Sum(p => p.Rounds));
    }

    [Fact]
    public void Load_StartsEmptyFromAnUnreadableFile()
    {
        File.WriteAllText(_path, "{ not json");

        HealthStore store = NewStore(persisted: true);

        Assert.Equal(0, store.LogOffset);
        Assert.Empty(store.Query("24h").Notes);
    }

    [Fact]
    public void Tailer_FeedsTheStoreOnceAcrossRestarts()
    {
        string log = Path.Combine(Path.GetTempPath(), $"func-node-{Guid.NewGuid():N}.txt");
        try
        {
            string[] lines = File.ReadAllLines(Fixture.NodeLog);
            File.WriteAllLines(log, lines.Take(100));

            // First run: read what is there, then FUNC stops.
            HealthStore store = NewStore(persisted: true);
            LogTailer tailer = new();
            tailer.Read(log, line => { if (LogParser.Parse(line) is { } ev) store.Apply(ev); }, long.MaxValue);
            store.SetLog(true, tailer.Offset, tailer.Fingerprint);
            store.Save();

            // Second run: the node has logged more in the meantime.
            File.AppendAllLines(log, lines.Skip(100));
            HealthStore restarted = NewStore(persisted: true);
            LogTailer resumed = new() { Offset = restarted.LogOffset, Fingerprint = restarted.LogFingerprint };
            resumed.Read(log, line => { if (LogParser.Parse(line) is { } ev) restarted.Apply(ev); }, long.MaxValue);

            Assert.Equal(new FileInfo(log).Length, resumed.Offset);
            Assert.Equivalent(FixtureStore().Query("24h").Points, restarted.Query("24h").Points, strict: true);
        }
        finally
        {
            File.Delete(log);
        }
    }
}
