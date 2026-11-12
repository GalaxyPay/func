using System.Text.Json;
using System.Text.Json.Serialization;
using FUNC.Models;

namespace FUNC.Health
{
    // One node's health history: per-minute totals for the last day and per-hour totals
    // for the last 30, built from node.log events and /metrics samples, and kept in a
    // JSON file so it survives FUNC restarts and log rotation.
    public sealed class HealthStore
    {
        public const long MinuteRetention = 24 * 3600;
        public const long HourRetention = 30 * 24 * 3600;
        private const int MaxNotes = 200;
        private static readonly TimeSpan StaleAfter = TimeSpan.FromSeconds(60);
        private static readonly TimeSpan QuietAfter = TimeSpan.FromMinutes(5);

        private static readonly JsonSerializerOptions Json = new() { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault };

        public sealed class Bucket
        {
            public long T { get; set; }
            public int Rounds { get; set; }
            // Sum and count of the measured gaps between consecutive rounds.
            public double RoundMs { get; set; }
            public int Intervals { get; set; }
            public double RoundMsMax { get; set; }
            public int Votes { get; set; }
            public int Proposals { get; set; }
            public int Wins { get; set; }
            public int Blocks { get; set; }
            public double ArrivalMs { get; set; }
            public double ArrivalMsMax { get; set; }
            public int Recoveries { get; set; }
            public int Connects { get; set; }
            public int Disconnects { get; set; }
            public int ConnectFails { get; set; }
            public int Warnings { get; set; }
            public int Errors { get; set; }
            // Latest peer counts sampled in the interval, and when.
            public double? PeersIn { get; set; }
            public double? PeersOut { get; set; }
            public long PeersT { get; set; }
            public double BytesIn { get; set; }
            public double BytesOut { get; set; }
            public double Dropped { get; set; }

            public void Add(Bucket o)
            {
                Rounds += o.Rounds;
                RoundMs += o.RoundMs;
                Intervals += o.Intervals;
                RoundMsMax = Math.Max(RoundMsMax, o.RoundMsMax);
                Votes += o.Votes;
                Proposals += o.Proposals;
                Wins += o.Wins;
                Blocks += o.Blocks;
                ArrivalMs += o.ArrivalMs;
                ArrivalMsMax = Math.Max(ArrivalMsMax, o.ArrivalMsMax);
                Recoveries += o.Recoveries;
                Connects += o.Connects;
                Disconnects += o.Disconnects;
                ConnectFails += o.ConnectFails;
                Warnings += o.Warnings;
                Errors += o.Errors;
                if (o.PeersT >= PeersT && (o.PeersIn != null || o.PeersOut != null))
                {
                    PeersIn = o.PeersIn;
                    PeersOut = o.PeersOut;
                    PeersT = o.PeersT;
                }
                BytesIn += o.BytesIn;
                BytesOut += o.BytesOut;
                Dropped += o.Dropped;
            }
        }

        private sealed class State
        {
            public long LogOffset { get; set; }
            public string? LogFingerprint { get; set; }
            public List<Bucket> Minutes { get; set; } = [];
            public List<Bucket> Hours { get; set; } = [];
            public List<HealthNote> Notes { get; set; } = [];
            public string? Version { get; set; }
            public long LastRound { get; set; }
            public DateTimeOffset? LastRoundAt { get; set; }
            public List<PeerInfo> Peers { get; set; } = [];
            public DateTimeOffset? PeersAt { get; set; }
            public Dictionary<long, List<string>> Proposed { get; set; } = [];
        }

        private readonly object _lock = new();
        private readonly string? _path;
        private readonly Func<DateTimeOffset> _now;

        private readonly Dictionary<long, Bucket> _minutes = [];
        private readonly Dictionary<long, Bucket> _hours = [];
        // Oldest first.
        private readonly List<HealthNote> _notes = [];
        private string? _version;
        private long _lastRound;
        private DateTimeOffset? _lastRoundAt;
        private List<PeerInfo> _peers = [];
        private DateTimeOffset? _peersAt;
        // This node's proposals for rounds not yet decided, by round. A round is
        // decided within seconds, so this holds a handful of entries at most.
        private readonly Dictionary<long, HashSet<string>> _proposed = [];
        private const int ProposalRounds = 20;

        // Not persisted: how the sources are doing right now.
        private bool _logFound;
        private DateTimeOffset? _logGrewAt;
        private DateTimeOffset? _watchingSince;
        private DateTimeOffset? _telemetryAt;
        private DateTimeOffset? _liveSince;
        private DateTimeOffset? _liveAt;
        private readonly CounterDeltas _liveDeltas = new();
        private double? _peersIn;
        private double? _peersOut;
        private DateTimeOffset _peerCountsAt;
        private DateTimeOffset? _startedAt;

        // Position in node.log up to which events are already counted.
        public long LogOffset { get; private set; }
        public string? LogFingerprint { get; private set; }

        // path: where to keep the history, or null to keep it in memory only.
        public HealthStore(string? path = null, Func<DateTimeOffset>? now = null)
        {
            _path = path;
            _now = now ?? (() => DateTimeOffset.UtcNow);
            Load();
        }

        #region Input

        public void Apply(HealthEvent ev)
        {
            lock (_lock)
            {
                long now = _now().ToUnixTimeSeconds();
                long t = ev.Time.ToUnixTimeSeconds();
                // Older than anything kept, or stamped by a clock that was badly off.
                if (t < now - HourRetention || t > now + 3600) return;
                if (ev.Version != null) _version = ev.Version;
                if (ev.Kind is not (HealthEventKind.RoundConcluded or HealthEventKind.Warning or HealthEventKind.Error)) _telemetryAt = _now();

                switch (ev.Kind)
                {
                    case HealthEventKind.RoundConcluded:
                        // Never count a round twice, e.g. when a log is read again.
                        if (ev.Round <= _lastRound) return;
                        double gap = ev.Round == _lastRound + 1 && _lastRoundAt != null ? (ev.Time - _lastRoundAt.Value).TotalMilliseconds : 0;
                        _lastRound = ev.Round;
                        _lastRoundAt = ev.Time;
                        Add(t, now, b =>
                        {
                            b.Rounds++;
                            if (ev.Period > 0) b.Recoveries++;
                            if (gap > 0)
                            {
                                b.RoundMs += gap;
                                b.Intervals++;
                                b.RoundMsMax = Math.Max(b.RoundMsMax, gap);
                            }
                        });
                        break;
                    case HealthEventKind.BlockAccepted:
                        // The round is decided: did the network take this node's block?
                        bool won = _proposed.Remove(ev.Round, out HashSet<string>? offered) && ev.Hash != null && offered.Contains(ev.Hash);
                        Add(t, now, b =>
                        {
                            b.Blocks++;
                            b.ArrivalMs += ev.ReceivedMs;
                            b.ArrivalMsMax = Math.Max(b.ArrivalMsMax, ev.ReceivedMs);
                            if (won) b.Wins++;
                        });
                        break;
                    case HealthEventKind.VoteSent:
                        Add(t, now, b => b.Votes++);
                        break;
                    case HealthEventKind.BlockProposed:
                        Add(t, now, b => b.Proposals++);
                        if (ev.Hash != null)
                        {
                            if (!_proposed.TryGetValue(ev.Round, out HashSet<string>? hashes)) _proposed[ev.Round] = hashes = [];
                            hashes.Add(ev.Hash);
                            // Rounds whose outcome never showed up in the log.
                            foreach (long r in _proposed.Keys.Where(r => r < ev.Round - ProposalRounds).ToList()) _proposed.Remove(r);
                        }
                        break;
                    case HealthEventKind.ConnectPeer:
                        Add(t, now, b => b.Connects++);
                        break;
                    case HealthEventKind.ConnectPeerFail:
                        Add(t, now, b => b.ConnectFails++);
                        break;
                    case HealthEventKind.DisconnectPeer:
                        Add(t, now, b => b.Disconnects++);
                        // algod swaps out its slowest relay every few minutes; that is routine.
                        if (ev.Text?.EndsWith("(LeastPerformingPeer)") != true) Note(ev.Time, "info", $"Peer disconnected: {ev.Text}");
                        break;
                    case HealthEventKind.PeerConnections:
                        if (_peersAt == null || ev.Time >= _peersAt)
                        {
                            _peers = ev.Peers ?? [];
                            _peersAt = ev.Time;
                        }
                        break;
                    case HealthEventKind.Heartbeat:
                        if (ev.Metrics != null) ApplyMetricsLocked(ev.Time, ev.Metrics, live: false);
                        break;
                    case HealthEventKind.Startup:
                        Note(ev.Time, "info", ev.Version == null ? "Node started" : $"Node started (version {ev.Version})");
                        break;
                    case HealthEventKind.CatchupStart:
                        Note(ev.Time, "info", $"Catching up from round {ev.Round}");
                        break;
                    case HealthEventKind.CatchupStop:
                        Note(ev.Time, "info", $"Caught up to round {ev.Round}");
                        break;
                    case HealthEventKind.Warning:
                        Add(t, now, b => b.Warnings++);
                        Note(ev.Time, "warning", ev.Text ?? "");
                        break;
                    case HealthEventKind.Error:
                        Add(t, now, b => b.Errors++);
                        Note(ev.Time, "error", ev.Text ?? "");
                        break;
                }
            }
        }

        // A sample of algod's metrics: scraped from /metrics just now (live), or as
        // recorded in a log heartbeat every ten minutes.
        public void ApplyMetrics(DateTimeOffset time, IReadOnlyDictionary<string, double> metrics, bool live)
        {
            lock (_lock) ApplyMetricsLocked(time, metrics, live);
        }

        private void ApplyMetricsLocked(DateTimeOffset time, IReadOnlyDictionary<string, double> metrics, bool live)
        {
            if (live)
            {
                _liveSince ??= time;
                _liveAt = time;
            }
            // Heartbeats only fill in the time before live samples began.
            else if (_liveSince != null && time >= _liveSince) return;

            long now = _now().ToUnixTimeSeconds();
            long t = time.ToUnixTimeSeconds();
            if (t < now - HourRetention || t > now + 3600) return;

            double? peersIn = Metric(metrics, "algod_network_incoming_peers");
            double? peersOut = Metric(metrics, "algod_network_outgoing_peers");
            // Traffic needs closely spaced samples to chart, so it is live-only.
            double bytesIn = 0, bytesOut = 0, dropped = 0;
            if (live)
            {
                if (Metric(metrics, "algod_network_received_bytes_total") is { } received) bytesIn = _liveDeltas.Next("in", received);
                if (Metric(metrics, "algod_network_sent_bytes_total") is { } sent) bytesOut = _liveDeltas.Next("out", sent);
                dropped = _liveDeltas.Next("dropped",
                    (Metric(metrics, "algod_agreement_dropped_vote") ?? 0)
                    + (Metric(metrics, "algod_agreement_dropped_proposal") ?? 0)
                    + (Metric(metrics, "algod_agreement_dropped_bundle") ?? 0));
            }

            Add(t, now, b =>
            {
                if ((peersIn != null || peersOut != null) && t >= b.PeersT)
                {
                    b.PeersIn = peersIn;
                    b.PeersOut = peersOut;
                    b.PeersT = t;
                }
                b.BytesIn += bytesIn;
                b.BytesOut += bytesOut;
                b.Dropped += dropped;
            });
            if ((peersIn != null || peersOut != null) && time >= _peerCountsAt)
            {
                _peersIn = peersIn;
                _peersOut = peersOut;
                _peerCountsAt = time;
            }
        }

        private static double? Metric(IReadOnlyDictionary<string, double> metrics, string name) =>
            metrics.TryGetValue(name, out double value) ? value : null;

        // The collector's report after each look at node.log. grew: lines were added
        // since the previous look (working through a backlog doesn't count).
        public void SetLog(bool found, long offset = 0, string? fingerprint = null, bool grew = false, DateTimeOffset? lastWrite = null)
        {
            lock (_lock)
            {
                _logFound = found;
                if (!found) return;
                LogOffset = offset;
                LogFingerprint = fingerprint;
                DateTimeOffset now = _now();
                _watchingSince ??= now;
                if (grew) _logGrewAt = now;
                // Until growth is seen, go by when the file was last written.
                else if (lastWrite != null && (_logGrewAt == null || lastWrite > _logGrewAt)) _logGrewAt = lastWrite < now ? lastWrite : now;
            }
        }

        // When the node's algod process started, if it is running.
        public void SetStartedAt(DateTimeOffset? startedAt)
        {
            lock (_lock) _startedAt = startedAt;
        }

        private void Add(long t, long now, Action<Bucket> update)
        {
            update(BucketAt(_hours, t - t % 3600));
            if (t >= now - MinuteRetention) update(BucketAt(_minutes, t - t % 60));
        }

        private static Bucket BucketAt(Dictionary<long, Bucket> buckets, long t)
        {
            if (!buckets.TryGetValue(t, out Bucket? bucket)) buckets[t] = bucket = new Bucket { T = t };
            return bucket;
        }

        private void Note(DateTimeOffset time, string level, string text)
        {
            // A node in trouble repeats itself; keep one entry per run of the same message.
            if (_notes.Count > 0 && _notes[^1] is { } last && last.Level == level && last.Text == text)
            {
                last.Count++;
                last.Time = time;
                return;
            }
            _notes.Add(new HealthNote { Time = time, Level = level, Text = text });
            if (_notes.Count > MaxNotes) _notes.RemoveRange(0, _notes.Count - MaxNotes);
        }

        #endregion

        #region Output

        public NodeHealth Query(string? range)
        {
            (int width, long span, bool hourly) = range switch
            {
                "1h" => (60, 3600L, false),
                null or "" or "24h" => (900, 24 * 3600L, false),
                "7d" => (3600, 7 * 24 * 3600L, true),
                "30d" => (6 * 3600, 30 * 24 * 3600L, true),
                _ => throw new Exception("Range must be 1h, 24h, 7d or 30d"),
            };

            lock (_lock)
            {
                DateTimeOffset now = _now();
                long nowSeconds = now.ToUnixTimeSeconds();
                long end = nowSeconds - nowSeconds % width + width;
                long start = end - span;

                Bucket[] merged = new Bucket[span / width];
                for (int i = 0; i < merged.Length; i++) merged[i] = new Bucket { T = start + (long)i * width };
                foreach (Bucket b in (hourly ? _hours : _minutes).Values)
                    if (b.T >= start && b.T < end) merged[(b.T - start) / width].Add(b);

                return new NodeHealth
                {
                    Source = SourceLocked(now),
                    Metrics = _liveAt != null && now - _liveAt < StaleAfter,
                    Version = _version,
                    StartedAt = _startedAt,
                    LastRound = _lastRound,
                    LastRoundAt = _lastRoundAt,
                    PeersIn = _peersIn,
                    PeersOut = _peersOut,
                    BucketSeconds = width,
                    Points = [.. merged.Select(Point)],
                    Peers = _peers,
                    PeersAt = _peersAt,
                    Notes = [.. Enumerable.Reverse(_notes)],
                };
            }
        }

        private string SourceLocked(DateTimeOffset now)
        {
            if (!_logFound) return "noLog";
            if (_logGrewAt == null || now - _logGrewAt > StaleAfter) return "stale";
            // A busy log with no telemetry in it for minutes: TelemetryToLog is off.
            bool watchedLongEnough = _watchingSince != null && now - _watchingSince > QuietAfter;
            bool quiet = _telemetryAt == null || now - _telemetryAt > QuietAfter;
            return watchedLongEnough && quiet ? "noEvents" : "ok";
        }

        private static HealthPoint Point(Bucket b) => new()
        {
            T = b.T,
            Rounds = b.Rounds,
            RoundMs = b.Intervals > 0 ? b.RoundMs / b.Intervals : null,
            RoundMsMax = b.RoundMsMax,
            Votes = b.Votes,
            Proposals = b.Proposals,
            Wins = b.Wins,
            Blocks = b.Blocks,
            ArrivalMs = b.Blocks > 0 ? b.ArrivalMs / b.Blocks : null,
            ArrivalMsMax = b.ArrivalMsMax,
            Recoveries = b.Recoveries,
            Connects = b.Connects,
            Disconnects = b.Disconnects,
            ConnectFails = b.ConnectFails,
            Warnings = b.Warnings,
            Errors = b.Errors,
            PeersIn = b.PeersIn,
            PeersOut = b.PeersOut,
            BytesIn = b.BytesIn,
            BytesOut = b.BytesOut,
            Dropped = b.Dropped,
        };

        #endregion

        #region Storage

        public void Save()
        {
            if (_path == null) return;
            string json;
            lock (_lock)
            {
                Prune();
                json = JsonSerializer.Serialize(new State
                {
                    LogOffset = LogOffset,
                    LogFingerprint = LogFingerprint,
                    Minutes = [.. _minutes.Values],
                    Hours = [.. _hours.Values],
                    Notes = _notes,
                    Version = _version,
                    LastRound = _lastRound,
                    LastRoundAt = _lastRoundAt,
                    Peers = _peers,
                    PeersAt = _peersAt,
                    // Saved so a win isn't missed when FUNC restarts mid-round.
                    Proposed = _proposed.ToDictionary(p => p.Key, p => p.Value.ToList()),
                }, Json);
            }
            // Replace the file in one step so a crash can't leave half of it.
            string tmp = _path + ".tmp";
            File.WriteAllText(tmp, json);
            File.Move(tmp, _path, true);
        }

        private void Load()
        {
            if (_path == null) return;
            State? state;
            try { state = JsonSerializer.Deserialize<State>(File.ReadAllText(_path), Json); } catch { return; }
            if (state == null) return;
            LogOffset = state.LogOffset;
            LogFingerprint = state.LogFingerprint;
            foreach (Bucket b in state.Minutes) _minutes[b.T] = b;
            foreach (Bucket b in state.Hours) _hours[b.T] = b;
            _notes.AddRange(state.Notes);
            _version = state.Version;
            _lastRound = state.LastRound;
            _lastRoundAt = state.LastRoundAt;
            _peers = state.Peers;
            _peersAt = state.PeersAt;
            foreach (var (round, hashes) in state.Proposed) _proposed[round] = [.. hashes];
            Prune();
        }

        private void Prune()
        {
            long now = _now().ToUnixTimeSeconds();
            foreach (long t in _minutes.Keys.Where(t => t < now - MinuteRetention).ToList()) _minutes.Remove(t);
            foreach (long t in _hours.Keys.Where(t => t < now - HourRetention).ToList()) _hours.Remove(t);
        }

        #endregion
    }
}
