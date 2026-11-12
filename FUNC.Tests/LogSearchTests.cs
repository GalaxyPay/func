using FUNC.Health;

namespace FUNC.Tests;

public class LogSearchTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"func-search-{Guid.NewGuid():N}.txt");

    public void Dispose() => File.Delete(_path);

    private static string Entry(int i, string level = "info", string msg = "hello") =>
        $"{{\"file\":\"x.go\",\"level\":\"{level}\",\"msg\":\"{msg} {i}\",\"time\":\"2026-11-11T15:00:{i % 60:00}.000000Z\"}}";

    // Every page of a search, following the cursor to the start of the file.
    private static List<LogLine> All(string path, string? query, string? level, int limit, int chunkBytes = 4096, long scanBytes = long.MaxValue)
    {
        List<LogLine> lines = [];
        long? before = null;
        for (int guard = 0; guard < 100_000; guard++)
        {
            LogPage page = LogSearch.Search(path, query, level, before, null, limit, chunkBytes, scanBytes);
            Assert.True(page.Lines.Count <= limit);
            lines.AddRange(page.Lines);
            if (page.Next == null) return lines;
            Assert.True(before == null || page.Next < before, "the cursor must move back");
            before = page.Next;
        }
        throw new Exception("Search did not finish");
    }

    [Fact]
    public void Search_ReturnsWarningsNewestFirstByDefault()
    {
        LogPage page = LogSearch.Search(Fixture.NodeLog, null, null, null, null, 200);

        Assert.True(page.Found);
        Assert.Equal(12, page.Lines.Count);
        Assert.Null(page.Next);
        Assert.All(page.Lines, l => Assert.Equal("warning", l.Level));
        Assert.Equal(page.Lines.OrderByDescending(l => l.Offset).Select(l => l.Offset), page.Lines.Select(l => l.Offset));
        Assert.Equal("AssembleBlock: assembled block round did not catch up to requested round: 66867203 != 66867204", page.Lines[0].Msg);
        Assert.Equal(DateTimeOffset.Parse("2026-11-11T16:13:40.629373-07:00"), page.Lines[0].Time);
        Assert.StartsWith("{\"file\":\"transactionPool.go\"", page.Lines[0].Raw);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(50)]
    public void Search_PagesThroughTheWholeFile(int limit)
    {
        string[] expected = [.. File.ReadAllLines(Fixture.NodeLog).Where(l => l.Length > 0).Reverse()];

        // Chunks far smaller than the fixture's longest lines (a heartbeat is ~6 KB).
        List<LogLine> lines = All(Fixture.NodeLog, null, "info", limit, chunkBytes: 1000);

        Assert.Equal(expected, lines.Select(l => l.Raw));
        Assert.Equal(lines.Count, lines.Select(l => l.Offset).Distinct().Count());
    }

    [Fact]
    public void Search_ContinuesAfterRunningOutOfBudget()
    {
        // Each request may read only 3 KB, so most return little or nothing.
        List<LogLine> lines = All(Fixture.NodeLog, null, "warning", 200, chunkBytes: 1000, scanBytes: 3000);

        Assert.Equal(12, lines.Count);
        LogPage first = LogSearch.Search(Fixture.NodeLog, null, "warning", null, null, 200, 1000, 3000);
        Assert.NotNull(first.Next);
    }

    [Theory]
    [InlineData("dbatomic", "warning", 6)]
    [InlineData("DBATOMIC", "warning", 6)]
    [InlineData("/agreement/votesent", "info", 57)]
    [InlineData("r-x1.algorand-mainnet.network", "info", 1)]
    [InlineData("no such text anywhere", "info", 0)]
    [InlineData(null, "error", 0)]
    [InlineData("  ", "warning", 12)]
    public void Search_FiltersByTextAndLevel(string? query, string level, int count)
    {
        Assert.Equal(count, All(Fixture.NodeLog, query, level, 200).Count);
    }

    [Fact]
    public void Search_OrdersLevelsBySeverity()
    {
        File.WriteAllLines(_path, [Entry(1, "debug"), Entry(2, "info"), Entry(3, "warning"), Entry(4, "error"), Entry(5, "fatal"), Entry(6, "panic"), "not json at all"]);

        Assert.Equal(7, All(_path, null, "info", 50).Count);
        Assert.Equal(["panic", "fatal", "error", "warning"], All(_path, null, "warning", 50).Select(l => l.Level));
        Assert.Equal(["panic", "fatal", "error"], All(_path, null, "error", 50).Select(l => l.Level));
        // A line that isn't JSON is shown as written.
        LogLine plain = All(_path, "not json", "info", 50).Single();
        Assert.Equal(("info", "not json at all", null), (plain.Level, plain.Msg, plain.Time));
    }

    [Fact]
    public void Search_IgnoresALevelMentionedInsideAMessage()
    {
        File.WriteAllLines(_path, [Entry(1, "info", "peer said \\\"level\\\":\\\"error\\\"")]);

        Assert.Empty(All(_path, null, "warning", 50));
        Assert.Single(All(_path, null, "info", 50));
    }

    [Fact]
    public void Search_LeavesOutALineStillBeingWritten()
    {
        File.WriteAllText(_path, Entry(1) + "\n" + Entry(2) + "\n" + Entry(3)[..20]);

        LogPage page = LogSearch.Search(_path, null, "info", null, null, 50);

        Assert.Equal(["hello 2", "hello 1"], page.Lines.Select(l => l.Msg));
        Assert.Equal(Entry(1).Length + Entry(2).Length + 2, page.End);
    }

    [Fact]
    public void Search_ReturnsOnlyNewerLinesWhenFollowing()
    {
        File.WriteAllLines(_path, [Entry(1), Entry(2)]);
        LogPage first = LogSearch.Search(_path, null, "info", null, null, 50);
        Assert.Equal(new FileInfo(_path).Length, first.End);

        Assert.Empty(LogSearch.Search(_path, null, "info", null, first.End, 50).Lines);

        File.AppendAllLines(_path, [Entry(3), Entry(4, "warning")]);
        LogPage next = LogSearch.Search(_path, null, "info", null, first.End, 50);

        Assert.Equal(["hello 4", "hello 3"], next.Lines.Select(l => l.Msg));
        Assert.Null(next.Next);
        Assert.Equal(new FileInfo(_path).Length, next.End);
        Assert.False(next.Reset);
    }

    [Fact]
    public void Search_AsksToStartOverAfterRotation()
    {
        File.WriteAllLines(_path, [Entry(1)]);

        LogPage page = LogSearch.Search(_path, null, "info", null, after: 5_000_000, 50);

        Assert.True(page.Reset);
        Assert.Empty(page.Lines);
    }

    [Fact]
    public void Search_HandlesWindowsLineEndings()
    {
        File.WriteAllText(_path, Entry(1) + "\r\n" + Entry(2) + "\r\n");

        Assert.Equal([Entry(2), Entry(1)], All(_path, null, "info", 50).Select(l => l.Raw));
    }

    [Fact]
    public void Search_CutsVeryLongLinesShort()
    {
        File.WriteAllLines(_path, [Entry(1), $"{{\"level\":\"warning\",\"msg\":\"{new string('x', 50_000)}\",\"time\":\"2026-11-11T15:00:00Z\"}}", Entry(3)]);

        List<LogLine> lines = All(_path, null, "info", 50);

        Assert.Equal(3, lines.Count);
        Assert.Equal(("warning", 501, 20_001), (lines[1].Level, lines[1].Msg.Length, lines[1].Raw.Length));
    }

    [Fact]
    public void Search_ReportsAMissingLog()
    {
        LogPage page = LogSearch.Search(_path, null, null, null, null, 50);

        Assert.False(page.Found);
        Assert.Empty(page.Lines);
    }

    [Fact]
    public void Search_ReadsALogTheNodeHoldsOpen()
    {
        using FileStream node = new(_path, FileMode.Create, FileAccess.Write, FileShare.Read | FileShare.Delete);
        node.Write(System.Text.Encoding.UTF8.GetBytes(Entry(1, "error") + "\n"));
        node.Flush();

        Assert.Single(LogSearch.Search(_path, null, "error", null, null, 50).Lines);
    }

    [Theory]
    [InlineData("verbose")]
    [InlineData("WARNING")]
    public void Search_RejectsUnknownLevels(string level)
    {
        Assert.Equal("Level must be info, warning or error", Assert.Throws<Exception>(() => LogSearch.Search(_path, null, level, null, null, 50)).Message);
    }

    [Fact]
    public void Search_LimitsItsInputs()
    {
        File.WriteAllLines(_path, Enumerable.Range(0, 1200).Select(i => Entry(i)));

        Assert.Equal(LogSearch.MaxLimit, LogSearch.Search(_path, null, "info", null, null, 1_000_000).Lines.Count);
        Assert.Single(LogSearch.Search(_path, null, "info", null, null, -3).Lines);
        Assert.Equal("Search text is too long", Assert.Throws<Exception>(() => LogSearch.Search(_path, new string('q', 201), "info", null, null, 50)).Message);
    }
}
