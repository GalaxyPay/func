using System.Text.Json;
using FUNC.Health;
using Microsoft.Extensions.Logging.Abstractions;

namespace FUNC.Tests;

[Collection("AppData")]
public class HealthCollectorTests
{
    private static async Task WaitFor(Func<bool> condition, string what)
    {
        for (int i = 0; i < 100 && !condition(); i++) await Task.Delay(100);
        Assert.True(condition(), $"Timed out waiting until {what}");
    }

    [Fact]
    public async Task Collector_FollowsANodeLogAndSavesOnStop()
    {
        // The collector's history for a node lives for the whole test run, so this is
        // the only test that runs it. It asserts on positions, not on event counts:
        // the fixture's events age out of the history as the calendar moves on.
        string dir = TestEnv.ResetNodeDir("voi");
        string log = Path.Combine(dir, "node.log");
        string saved = Path.Combine(TestEnv.DataDir, "health-voi.json");
        string[] lines = File.ReadAllLines(Fixture.NodeLog);
        File.WriteAllLines(log, lines.Take(120));
        HealthStore store = HealthCollector.Store("voi");
        Assert.Equal("noLog", store.Query("1h").Source);

        HealthCollector collector = new(NullLogger<HealthCollector>.Instance);
        await collector.StartAsync(CancellationToken.None);
        try
        {
            // Catches up with what the node logged before FUNC started. That file was
            // written just now, so the log counts as live.
            await WaitFor(() => store.LogOffset == new FileInfo(log).Length, "the existing log is read");
            Assert.Equal("ok", store.Query("1h").Source);
            Assert.NotNull(store.LogFingerprint);

            // Then picks up lines as the node adds them.
            File.AppendAllLines(log, lines.Skip(120));
            await WaitFor(() => store.LogOffset == new FileInfo(log).Length, "the new lines are read");
        }
        finally
        {
            await collector.StopAsync(CancellationToken.None);
        }

        // Stopping saves the history together with the position it corresponds to.
        using JsonDocument state = JsonDocument.Parse(File.ReadAllText(saved));
        Assert.Equal(new FileInfo(log).Length, state.RootElement.GetProperty("LogOffset").GetInt64());
        Assert.Equal(store.LogFingerprint, state.RootElement.GetProperty("LogFingerprint").GetString());
    }
}
