using System.Collections.Concurrent;
using System.Diagnostics;

namespace FUNC.Health
{
    // Keeps each node's health history up to date in the background: follows node.log
    // for agreement, peer and warning events, and samples algod's /metrics for peer
    // counts and traffic. Nothing here changes the node or its configuration.
    public sealed class HealthCollector(ILogger<HealthCollector> logger) : BackgroundService
    {
        private static readonly string[] Nodes = ["algorand", "voi"];
        private static readonly TimeSpan LogPoll = TimeSpan.FromSeconds(2);
        private static readonly TimeSpan IdlePoll = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan MetricsPoll = TimeSpan.FromSeconds(15);
        private static readonly TimeSpan SaveInterval = TimeSpan.FromMinutes(5);
        // Most log to take in before looking up again, so a long backlog stays stoppable.
        private const long ChunkBytes = 32 * 1024 * 1024;

        private static readonly ConcurrentDictionary<string, HealthStore> Stores = new();

        public static HealthStore Store(string name) =>
            Stores.GetOrAdd(name, n => new HealthStore(Path.Combine(Utils.appDataDir, $"health-{n}.json")));

        protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
            Task.WhenAll(Nodes.SelectMany(name => new[]
            {
                Task.Run(() => FollowLog(name, stoppingToken), CancellationToken.None),
                Task.Run(() => SampleMetrics(name, stoppingToken), CancellationToken.None),
            }));

        private async Task FollowLog(string name, CancellationToken ct)
        {
            HealthStore store = Store(name);
            LogTailer tailer = new() { Offset = store.LogOffset, Fingerprint = store.LogFingerprint };
            DateTime saved = DateTime.UtcNow;
            bool caughtUp = false;
            // History is saved only from this loop, between reads, so the saved log
            // position always matches the events counted so far.
            while (!ct.IsCancellationRequested)
            {
                TimeSpan wait = LogPoll;
                try
                {
                    string path = Path.Combine(Node.DataDir(name), "node.log");
                    if (!File.Exists(path))
                    {
                        store.SetLog(false);
                        wait = IdlePoll;
                    }
                    else
                    {
                        long read = tailer.Read(path, line =>
                        {
                            if (LogParser.Parse(line) is { } ev) store.Apply(ev);
                        }, ChunkBytes);
                        store.SetLog(true, tailer.Offset, tailer.Fingerprint, grew: caughtUp && read > 0, File.GetLastWriteTimeUtc(path));
                        // Whatever is read before first reaching the end was already there.
                        if (read >= ChunkBytes) wait = TimeSpan.Zero;
                        else caughtUp = true;
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning("Reading the {Name} node log failed: {Message}", name, ex.Message);
                    wait = IdlePoll;
                }

                if (DateTime.UtcNow - saved > SaveInterval)
                {
                    Save(store, name);
                    saved = DateTime.UtcNow;
                }
                try { await Task.Delay(wait, ct); } catch (OperationCanceledException) { }
            }
            Save(store, name);
        }

        private void Save(HealthStore store, string name)
        {
            try { store.Save(); }
            catch (Exception ex) { logger.LogWarning("Saving {Name} node health failed: {Message}", name, ex.Message); }
        }

        private static async Task SampleMetrics(string name, CancellationToken ct)
        {
            HealthStore store = Store(name);
            using HttpClient http = new() { Timeout = TimeSpan.FromSeconds(5) };
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    store.SetStartedAt(AlgodStartTime(name));
                    int port = Node.AlgodPort(name);
                    string token = Node.AlgodToken(name);
                    if (port != 0 && token.Length > 0)
                    {
                        using HttpRequestMessage request = new(HttpMethod.Get, $"http://localhost:{port}/metrics");
                        request.Headers.Add("X-Algo-Api-Token", token);
                        using HttpResponseMessage response = await http.SendAsync(request, ct);
                        if (response.IsSuccessStatusCode)
                            store.ApplyMetrics(DateTimeOffset.UtcNow, Prometheus.Parse(await response.Content.ReadAsStringAsync(ct)), live: true);
                    }
                }
                catch
                {
                    // No node, a stopped node or one still starting: nothing to sample.
                }
                try { await Task.Delay(MetricsPoll, ct); } catch (OperationCanceledException) { }
            }
        }

        // algod records its process id in the data dir while it runs.
        private static DateTimeOffset? AlgodStartTime(string name)
        {
            try
            {
                int pid = int.Parse(File.ReadAllText(Path.Combine(Node.DataDir(name), "algod.pid")).Trim());
                using Process process = Process.GetProcessById(pid);
                if (!process.ProcessName.Contains("algod", StringComparison.OrdinalIgnoreCase)) return null;
                return process.StartTime;
            }
            catch
            {
                return null;
            }
        }
    }
}
