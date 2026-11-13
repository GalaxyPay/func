using System.Diagnostics;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using FUNC.Controllers;
using FUNC.Health;
using FUNC.Models;
using Microsoft.AspNetCore.Http;
using static System.OperatingSystem;

namespace FUNC
{
    // Builds the support snapshot served at GET func/diagnostics. It has to work when
    // things are broken, so each probe is isolated: one that fails records an error and
    // the rest of the report still fills in.
    public static partial class Diagnostics
    {
        // config.json keys worth seeing when troubleshooting. Anything else in the file
        // is left out, even if it looks harmless.
        internal static readonly string[] ConfigKeys =
        [
            "Version", "EndpointAddress", "BaseLoggerDebugLevel", "Archival", "EnableFollowMode",
            "EnableP2P", "EnableP2PHybridMode", "DNSBootstrapID", "CatchupParallelBlocks",
            "EnableDeveloperAPI", "IsIndexerActive",
        ];

        private const int MaxMessages = 25;
        private const int LogSearchLimit = 1000;
        // Each search reads at most 64 MB back. A busy node logs that much in minutes, so
        // the warnings cover a shorter window there.
        private const int MaxLogSearches = 4;
        private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(3);

        public static async Task<DiagnosticsReport> Collect(bool corsConfigured)
        {
            DiagnosticsReport report = new() { Func = FuncInfo(corsConfigured) };

            // Adopting an apt/NodeKit node changes where the algorand node lives.
            try { await Node.SyncExternal(); } catch { }

            report.Nodes["algorand"] = await CollectNode("algorand");
            // A Voi entry for a node that was never installed only muddies Algorand troubleshooting.
            string voiStatus = await Safe(() => Node.ServiceStatus("voi"), "Unknown");
            if (voiStatus != "Not Found") report.Nodes["voi"] = await CollectNode("voi", voiStatus);
            return report;
        }

        // Whether the caller may read the report: anyone on the node machine itself, so a
        // user locked out of the dashboard can still open it in a browser there, or anyone
        // signed in. A reverse proxy on the same machine makes every visitor look local,
        // so a forwarded request counts as remote.
        public static bool Allowed(HttpRequest request)
        {
            bool forwarded = request.Headers.ContainsKey("X-Forwarded-For")
                || request.Headers.ContainsKey("Forwarded")
                || request.Headers.ContainsKey("X-Real-IP");
            if (!forwarded && Auth.IsLoopback(request.HttpContext.Connection.RemoteIpAddress)) return true;
            return Auth.ValidateSession(Auth.BearerToken(request));
        }

        private static FuncDiagnostics FuncInfo(bool corsConfigured)
        {
            FuncDiagnostics info = new()
            {
                Os = RuntimeInformation.OSDescription,
                Arch = RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant(),
                Dotnet = RuntimeInformation.FrameworkDescription,
                PasswordSet = Auth.HasPassword,
                CorsConfigured = corsConfigured,
                AppDataDir = Redact(Utils.appDataDir),
            };
            try
            {
                // Release builds take their version from webui/package.json (CI's VERSION).
                string? version = Assembly.GetEntryAssembly()?
                    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
                info.Version = version?.Split('+')[0];
            }
            catch { }
            try
            {
                using Process p = Process.GetCurrentProcess();
                info.StartedAt = p.StartTime.ToUniversalTime();
            }
            catch { }
            return info;
        }

        private static async Task<NodeDiagnostics> CollectNode(string name, string? serviceStatus = null)
        {
            NodeDiagnostics node = new();

            await Probe(node, "service", async () =>
            {
                node.ServiceStatus = serviceStatus ?? await Node.ServiceStatus(name);
                if (node.ServiceStatus is "Running" or "Stopped") node.StartsAtBoot = await Node.StartsAtBoot(name);
                ExternalNode? ext = name == "algorand" ? External.Load() : null;
                node.ManagedBy = ext?.Manager ?? "func";
                node.Network = ext?.Network;
                if (name == "algorand" && ext == null && node.ServiceStatus == "Not Found")
                    node.DetectedExternal = (await External.Detect())?.Manager;
            });

            string dataDir = Node.DataDir(name);
            await Probe(node, "dataDir", () => node.DataDir = DataDirInfo(dataDir));
            await Probe(node, "config", () => node.Config = ConfigSummary(Path.Combine(dataDir, "config.json")));
            await Probe(node, "algod", async () => node.Algod = await AlgodInfo(name, node.ServiceStatus == "Running", node.Errors));
            await Probe(node, "health", () => node.Health = HealthInfo(name));
            // The day before the log was last written, not before now: for a node that stopped
            // a while ago, what it logged before stopping is what matters.
            string logPath = Path.Combine(dataDir, "node.log");
            DateTimeOffset until = LogEnd(logPath, DateTimeOffset.UtcNow);
            await Probe(node, "recentErrors", () => node.RecentErrors = RecentErrors(HealthCollector.Store(name).Query("24h").Notes, until));
            await Probe(node, "recentWarnings", () => node.RecentWarnings = RecentWarnings(logPath, until));

            if (name == "algorand")
            {
                await Probe(node, "telemetry", async () =>
                {
                    if (Directory.Exists(dataDir)) node.TelemetryEnabled = TelemetryEnabled(await Node.TelemetryStatus(name));
                });
                await Probe(node, "reti", async () => node.Reti = await Node.RetiStatus());
                await Probe(node, "valar", async () => node.Valar = await ValarController.GetStatus());
            }
            return node;
        }

        private static async Task Probe(NodeDiagnostics node, string section, Func<Task> probe)
        {
            try { await probe(); }
            catch (Exception ex) { node.Errors.Add($"{section}: {Redact(ex.Message)}"); }
        }

        private static Task Probe(NodeDiagnostics node, string section, Action probe) =>
            Probe(node, section, () => { probe(); return Task.CompletedTask; });

        private static async Task<T> Safe<T>(Func<Task<T>> f, T fallback)
        {
            try { return await f(); } catch { return fallback; }
        }

        internal static DataDirDiagnostics DataDirInfo(string dataDir)
        {
            DataDirDiagnostics info = new() { Path = Redact(dataDir), Exists = Directory.Exists(dataDir) };
            // Disk space of the volume the data dir is on (or would be on).
            string? existing = dataDir;
            while (existing != null && !Directory.Exists(existing)) existing = Path.GetDirectoryName(existing);
            if (existing != null)
            {
                try
                {
                    DriveInfo drive = new(IsWindows() ? Path.GetPathRoot(existing)! : existing);
                    info.FreeBytes = drive.AvailableFreeSpace;
                    info.TotalBytes = drive.TotalSize;
                }
                catch { }
            }
            FileInfo log = new(Path.Combine(dataDir, "node.log"));
            if (log.Exists)
            {
                info.NodeLogBytes = log.Length;
                info.NodeLogModifiedAt = log.LastWriteTimeUtc;
            }
            return info;
        }

        // The allowlisted settings from config.json; null when there is no file.
        internal static Dictionary<string, JsonNode?>? ConfigSummary(string configPath)
        {
            if (!File.Exists(configPath)) return null;
            JsonObject config = JsonNode.Parse(File.ReadAllText(configPath)) as JsonObject
                ?? throw new Exception("config.json is not a JSON object");
            Dictionary<string, JsonNode?> summary = [];
            foreach (string key in ConfigKeys)
            {
                if (!config.TryGetPropertyValue(key, out JsonNode? value)) continue;
                summary[key] = value is JsonValue v && v.TryGetValue(out string? s) ? JsonValue.Create(Redact(s)) : value?.DeepClone();
            }
            return summary;
        }

        // algod is only asked when its service runs; a stopped node would just time out.
        // Each request that fails is recorded on its own.
        private static async Task<AlgodDiagnostics> AlgodInfo(string name, bool running, List<string> errors)
        {
            AlgodDiagnostics info = new() { Port = Node.AlgodPort(name) };
            string token = Node.AlgodToken(name);
            info.TokenPresent = token.Length > 0;
            if (info.Port == 0 || !running) return info;

            using HttpClient http = new() { Timeout = ProbeTimeout };
            try
            {
                JsonObject? versions = await AlgodGet(http, info.Port, "/versions", null);
                if (versions?["build"] is JsonObject build)
                {
                    info.Version = $"{build["major"]}.{build["minor"]}.{build["build_number"]}";
                    info.Channel = build["channel"]?.ToString();
                }
                info.GenesisId = versions?["genesis_id"]?.ToString();
            }
            catch (Exception ex) { errors.Add($"algod /versions: {Redact(ex.Message)}"); }
            if (token.Length == 0) return info;
            try { info.Status = MillisecondStatus(await AlgodGet(http, info.Port, "/v2/status", token)); }
            catch (Exception ex) { errors.Add($"algod /v2/status: {Redact(ex.Message)}"); }
            return info;
        }

        // algod reports durations in nanoseconds; milliseconds are easier to read at a glance.
        internal static JsonObject? MillisecondStatus(JsonObject? status)
        {
            if (status == null) return null;
            foreach (string key in new[] { "time-since-last-round", "catchup-time" })
            {
                if (status[key] is JsonValue v && v.TryGetValue(out long ns))
                {
                    status.Remove(key);
                    status[$"{key}-ms"] = ns / 1_000_000;
                }
            }
            return status;
        }

        private static async Task<JsonObject?> AlgodGet(HttpClient http, int port, string path, string? token)
        {
            using HttpRequestMessage request = new(HttpMethod.Get, $"http://127.0.0.1:{port}{path}");
            if (token != null) request.Headers.Add("X-Algo-Api-Token", token);
            using HttpResponseMessage response = await http.SendAsync(request);
            string body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) throw new Exception($"{path} returned {(int)response.StatusCode}: {Redact(body.Trim())}");
            return JsonNode.Parse(body) as JsonObject;
        }

        private static HealthDiagnostics HealthInfo(string name)
        {
            NodeHealth health = HealthCollector.Store(name).Query("24h");
            return new HealthDiagnostics
            {
                Source = health.Source,
                Metrics = health.Metrics,
                StartedAt = health.StartedAt?.ToUniversalTime(),
                LastRound = health.LastRound,
                LastRoundAt = health.LastRoundAt?.ToUniversalTime(),
                LastVoteAt = health.LastVoteAt?.ToUniversalTime(),
                PeersIn = health.PeersIn,
                PeersOut = health.PeersOut,
                Warnings24h = health.Points.Sum(p => p.Warnings),
                Errors24h = health.Points.Sum(p => p.Errors),
            };
        }

        // When the node last wrote its log, or now if it has no log.
        internal static DateTimeOffset LogEnd(string logPath, DateTimeOffset now)
        {
            if (!File.Exists(logPath)) return now;
            DateTimeOffset written = File.GetLastWriteTimeUtc(logPath);
            return written < now ? written : now;
        }

        // Distinct errors from the 24 hours up to `until`, newest first. They come from the health
        // history, which follows the log continuously, so they cover the whole day even
        // though a busy node rotates node.log every few hours.
        internal static RecentMessages RecentErrors(IEnumerable<HealthNote> notes, DateTimeOffset until)
        {
            DateTimeOffset since = until - TimeSpan.FromHours(24);
            RecentMessages recent = new() { Since = since, Until = until };
            foreach (HealthNote note in notes.Where(n => n.Level == "error" && n.Time.ToUniversalTime() >= since && n.Time.ToUniversalTime() <= until).OrderBy(n => n.Time))
            {
                // A note is a run of one message; its Time is the latest in the run.
                recent.Count += note.Count;
                Tally(recent, Redact(note.Text), note.Time.ToUniversalTime(), note.Count);
            }
            Finish(recent);
            return recent;
        }

        // Distinct warnings from the node log, newest first. Warnings aren't kept in the
        // health history, so this searches node.log and then node.archive.log (where algod
        // moves the log when it rotates it). A busy node writes more than can be read back
        // quickly, so Since is the time of the oldest line actually read: 24 hours before Until at most.
        internal static RecentMessages RecentWarnings(string logPath, DateTimeOffset until, int maxSearches = MaxLogSearches,
            int chunkBytes = LogSearch.ChunkBytes, long scanBytes = LogSearch.ScanBytes)
        {
            DateTimeOffset since = until - TimeSpan.FromHours(24);
            RecentMessages recent = new() { Until = until };
            if (!File.Exists(logPath)) return recent;
            string[] files = [logPath, Path.Combine(Path.GetDirectoryName(logPath)!, "node.archive.log")];

            List<LogLine> lines = [];
            int searches = 0;
            bool reachedSince = false;
            // Where reading stopped: the oldest line read so far.
            string stopFile = logPath;
            long stopOffset = 0;
            foreach (string file in files.Where(File.Exists))
            {
                long? before = null;
                while (searches < maxSearches && !reachedSince)
                {
                    LogPage page = LogSearch.Search(file, null, "warning", before, null, LogSearchLimit, chunkBytes, scanBytes);
                    searches++;
                    lines.AddRange(page.Lines);
                    reachedSince = page.Lines.Any(l => l.Time?.ToUniversalTime() < since);
                    (stopFile, stopOffset) = (file, page.Next ?? 0);
                    if (page.Next == null) break;
                    before = page.Next;
                }
                if (searches >= maxSearches || reachedSince) break;
            }

            DateTimeOffset? stoppedAt = reachedSince ? since : LogSearch.TimeAt(stopFile, stopOffset)?.ToUniversalTime();
            recent.Since = stoppedAt is { } t && t > since ? t : since;

            foreach (LogLine line in Enumerable.Reverse(lines))
            {
                if (line.Level != "warning" || line.Time?.ToUniversalTime() is not { } time || time < recent.Since || time > until) continue;
                recent.Count++;
                Tally(recent, Redact(line.Msg), time, 1);
            }
            Finish(recent);
            return recent;
        }

        // Adds occurrences of a message, given oldest first.
        private static void Tally(RecentMessages recent, string msg, DateTimeOffset time, int count)
        {
            LogSummary? summary = recent.Messages.FirstOrDefault(m => m.Msg == msg);
            if (summary == null) recent.Messages.Add(summary = new LogSummary { Msg = msg, FirstAt = time });
            summary.Count += count;
            summary.LastAt = time;
        }

        private static void Finish(RecentMessages recent) =>
            recent.Messages = [.. recent.Messages.OrderByDescending(m => m.LastAt).Take(MaxMessages)];

        // `diagcfg telemetry status` prints e.g. "Remote logging is enabled.  Node = x, Guid = ...".
        internal static bool? TelemetryEnabled(string status)
        {
            if (string.IsNullOrWhiteSpace(status)) return null;
            if (status.Contains("disabled", StringComparison.OrdinalIgnoreCase)) return false;
            if (status.Contains("enabled", StringComparison.OrdinalIgnoreCase)) return true;
            return null;
        }

        // ---- Redaction ----

        // A user's home folder, which names them: C:\Users\<name>, /home/<name>, /Users/<name>.
        [GeneratedRegex(@"(?i)(?:\b[A-Z]:[\\/]Users[\\/]|(?<![\w.])/home/|(?<![\w.])/Users/)[^\\/\s""']+")]
        private static partial Regex HomeDir();

        [GeneratedRegex(@"(?<![\w.])(?:\d{1,3}\.){3}\d{1,3}(?!\w|\.\d)")]
        private static partial Regex Ipv4();

        // Bracketed IPv6, full IPv6, or compressed IPv6 (with "::").
        [GeneratedRegex(@"\[[0-9A-Fa-f.]*:[0-9A-Fa-f.]*:[0-9A-Fa-f:.%\w]*\]|(?<![\w:])(?:[0-9A-Fa-f]{1,4}:){7}[0-9A-Fa-f]{1,4}(?![\w:])|(?<![\w:])(?:[0-9A-Fa-f]{1,4}(?::[0-9A-Fa-f]{1,4})*)?::(?:[0-9A-Fa-f]{1,4}(?::[0-9A-Fa-f]{1,4})*)?(?![\w:])")]
        private static partial Regex Ipv6();

        [GeneratedRegex(@"(?i)\b[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}\b")]
        private static partial Regex GuidPattern();

        [GeneratedRegex(@"\b[A-Z2-7]{58}\b")]
        private static partial Regex AlgorandAddress();

        // Strips what could identify the user or their peers from text going into the
        // report. Loopback and unspecified addresses stay: whether algod binds to
        // 127.0.0.1 or 0.0.0.0 matters when troubleshooting and identifies no one.
        public static string Redact(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            text = HomeDir().Replace(text, "~");
            text = GuidPattern().Replace(text, "<guid>");
            text = Ipv6().Replace(text, m => KeepIp(m.Value.Trim('[', ']')) ? m.Value : "<ip>");
            text = Ipv4().Replace(text, m => KeepIp(m.Value) ? m.Value : "<ip>");
            text = AlgorandAddress().Replace(text, m => $"{m.Value[..4]}…{m.Value[^4..]}");
            return text;
        }

        private static bool KeepIp(string ip)
        {
            int zone = ip.IndexOf('%');
            if (zone >= 0) ip = ip[..zone];
            // Not an address after all (or a bare "::"): leave it.
            if (ip.Length < 3 || !IPAddress.TryParse(ip, out IPAddress? address)) return true;
            if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
            return IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any);
        }
    }
}
