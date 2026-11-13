using System.Net;
using System.Text.Json;
using FUNC.Health;
using FUNC.Models;
using Microsoft.AspNetCore.Http;

namespace FUNC.Tests;

[Collection("AppData")]
public class DiagnosticsTests : IDisposable
{
    private const string Address = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"func-diag-{Guid.NewGuid():N}");

    public DiagnosticsTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { }
    }

    // ---- Redaction ----

    [Theory]
    [InlineData(@"C:\Users\alice\node\data", @"~\node\data")]
    [InlineData("/home/bob/node/data", "~/node/data")]
    [InlineData("/Users/carol/Library/func", "~/Library/func")]
    [InlineData("/var/lib/algorand", "/var/lib/algorand")]
    public void Redact_HidesHomeFolders(string text, string expected) =>
        Assert.Equal(expected, Diagnostics.Redact(text));

    [Theory]
    [InlineData("peer 203.0.113.7:4160 disconnected", "peer <ip>:4160 disconnected")]
    [InlineData("dial ws://198.51.100.20:4160/v1/mainnet-v1.0/gossip", "dial ws://<ip>:4160/v1/mainnet-v1.0/gossip")]
    [InlineData("from [2001:db8::7]:4160", "from <ip>:4160")]
    [InlineData("from 2001:db8:0:0:0:0:0:7 now", "from <ip> now")]
    [InlineData("listening on 127.0.0.1:8080 and [::1]:8080", "listening on 127.0.0.1:8080 and [::1]:8080")]
    [InlineData("bound to 0.0.0.0:4160", "bound to 0.0.0.0:4160")]
    [InlineData("round took 15:00:01 at v3.27.0", "round took 15:00:01 at v3.27.0")]
    public void Redact_HidesAddressesButNotLoopback(string text, string expected) =>
        Assert.Equal(expected, Diagnostics.Redact(text));

    [Fact]
    public void Redact_ShortensAccountsAndHidesGuids()
    {
        string text = Diagnostics.Redact($"account {Address} key; guid 0f3c2b1a-1234-4abc-9def-0123456789ab");

        Assert.Equal("account AAAA…AAAA key; guid <guid>", text);
    }

    [Theory]
    [InlineData("Remote logging is enabled.  Node = x, Guid = 0f3c2b1a-1234-4abc-9def-0123456789ab", true)]
    [InlineData("Remote logging is currently disabled", false)]
    [InlineData("", null)]
    public void TelemetryEnabled_ReadsOnlyTheSwitch(string status, bool? expected) =>
        Assert.Equal(expected, Diagnostics.TelemetryEnabled(status));

    // ---- Config ----

    [Fact]
    public void ConfigSummary_KeepsOnlyAllowlistedKeys()
    {
        string path = Path.Combine(_dir, "config.json");
        File.WriteAllText(path, """
            { "Version": 36, "EndpointAddress": "192.168.1.5:8080", "EnableP2P": true,
              "SomeSecretThing": "hunter2", "TelemetryToHost": "/home/dave/x" }
            """);

        var summary = Diagnostics.ConfigSummary(path)!;

        Assert.Equal(["EnableP2P", "EndpointAddress", "Version"], summary.Keys.Order(StringComparer.Ordinal));
        Assert.Equal("<ip>:8080", summary["EndpointAddress"]!.ToString());
        Assert.Equal(36, summary["Version"]!.GetValue<int>());
    }

    [Fact]
    public void ConfigSummary_IsNullWithoutAFile() =>
        Assert.Null(Diagnostics.ConfigSummary(Path.Combine(_dir, "missing.json")));

    [Fact]
    public void ConfigSummary_ThrowsOnABrokenFile()
    {
        string path = Path.Combine(_dir, "config.json");
        File.WriteAllText(path, "{ not json");

        Assert.ThrowsAny<Exception>(() => Diagnostics.ConfigSummary(path));
    }

    // ---- Recent log ----

    private static string Entry(DateTimeOffset time, string level, string msg) =>
        JsonSerializer.Serialize(new { file = "x.go", level, msg, time = time.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.ffffffZ") });

    [Fact]
    public void RecentWarnings_GroupsAndRedactsNewestFirst()
    {
        DateTimeOffset now = new(2026, 11, 13, 12, 0, 0, TimeSpan.Zero);
        string path = Path.Combine(_dir, "node.log");
        File.WriteAllLines(path,
        [
            Entry(now.AddHours(-30), "warning", "too old to count"),
            Entry(now.AddHours(-3), "warning", "peer 203.0.113.7:4160 slow"),
            Entry(now.AddHours(-2), "error", "errors come from the health history"),
            Entry(now.AddHours(-1), "warning", "peer 198.51.100.9:4160 slow"),
            Entry(now.AddMinutes(-30), "info", "just chatter"),
            Entry(now.AddMinutes(-10), "warning", "newest warning"),
        ]);

        RecentMessages recent = Diagnostics.RecentWarnings(path, now);

        Assert.Equal(now.AddHours(-24), recent.Since);
        Assert.Equal(3, recent.Count);
        Assert.Equal(["newest warning", "peer <ip>:4160 slow"], recent.Messages.Select(m => m.Msg));
        LogSummary slow = recent.Messages[1];
        Assert.Equal(2, slow.Count);
        Assert.Equal(now.AddHours(-3), slow.FirstAt);
        Assert.Equal(now.AddHours(-1), slow.LastAt);
    }

    // A day of busy logging: one warning an hour, with plenty of info lines between.
    private string BusyLog(DateTimeOffset now)
    {
        string path = Path.Combine(_dir, "node.log");
        List<string> lines = [];
        for (int h = 30; h >= 1; h--)
        {
            lines.Add(Entry(now.AddHours(-h), "warning", "hourly warning"));
            for (int i = 0; i < 20; i++) lines.Add(Entry(now.AddHours(-h).AddMinutes(i), "info", new string('x', 100)));
        }
        File.WriteAllLines(path, lines);
        return path;
    }

    [Fact]
    public void RecentWarnings_SearchesBackAFullDay()
    {
        DateTimeOffset now = new(2026, 11, 13, 12, 0, 0, TimeSpan.Zero);
        string path = BusyLog(now);

        // Each search reads only ~20 KB, so covering the day takes several.
        RecentMessages recent = Diagnostics.RecentWarnings(path, now, maxSearches: 100, chunkBytes: 4096, scanBytes: 20_000);

        Assert.Equal(24, recent.Count);
        Assert.Equal(now.AddHours(-24), recent.Since);
    }

    [Fact]
    public void RecentWarnings_SinceIsWhereTheSearchStopped()
    {
        DateTimeOffset now = new(2026, 11, 13, 12, 0, 0, TimeSpan.Zero);
        string path = BusyLog(now);

        RecentMessages recent = Diagnostics.RecentWarnings(path, now, maxSearches: 2, chunkBytes: 4096, scanBytes: 20_000);

        // Where the search stopped, which is usually between warnings, not on one.
        Assert.NotNull(recent.Since);
        Assert.InRange(recent.Since!.Value, now.AddHours(-23), now.AddHours(-1));
        Assert.NotEqual(0, recent.Since.Value.Minute);
        Assert.Equal((int)(now - recent.Since.Value).TotalHours, recent.Count);
    }

    [Fact]
    public void RecentWarnings_IsEmptyWithoutALog()
    {
        RecentMessages recent = Diagnostics.RecentWarnings(Path.Combine(_dir, "node.log"), DateTimeOffset.UtcNow);

        Assert.Null(recent.Since);
        Assert.Equal(0, recent.Count);
        Assert.Empty(recent.Messages);
    }

    [Fact]
    public void RecentErrors_CoverTheDayFromHealthNotes()
    {
        DateTimeOffset now = new(2026, 11, 13, 12, 0, 0, TimeSpan.Zero);
        List<HealthNote> notes =
        [
            new() { Time = now.AddHours(-30), Level = "error", Text = "too old" },
            new() { Time = now.AddHours(-20), Level = "error", Text = "dial 203.0.113.7:4160 failed", Count = 3 },
            new() { Time = now.AddHours(-5), Level = "error", Text = "disk nearly full" },
            new() { Time = now.AddHours(-2), Level = "error", Text = "dial 198.51.100.9:4160 failed" },
        ];

        RecentMessages recent = Diagnostics.RecentErrors(notes, now);

        Assert.Equal(now.AddHours(-24), recent.Since);
        Assert.Equal(5, recent.Count);
        Assert.Equal(["dial <ip>:4160 failed", "disk nearly full"], recent.Messages.Select(m => m.Msg));
        Assert.Equal(4, recent.Messages[0].Count);
        Assert.Equal(now.AddHours(-20), recent.Messages[0].FirstAt);
        Assert.Equal(now.AddHours(-2), recent.Messages[0].LastAt);
    }

    [Fact]
    public void MillisecondStatus_ConvertsAlgodDurations()
    {
        var status = System.Text.Json.Nodes.JsonNode.Parse("""
            { "last-round": 5, "time-since-last-round": 1793125500, "catchup-time": 0 }
            """)!.AsObject();

        var result = Diagnostics.MillisecondStatus(status)!;

        Assert.Equal(1793, result["time-since-last-round-ms"]!.GetValue<long>());
        Assert.Equal(0, result["catchup-time-ms"]!.GetValue<long>());
        Assert.False(result.ContainsKey("time-since-last-round"));
        Assert.Equal(5, result["last-round"]!.GetValue<int>());
    }

    // ---- Data dir ----

    [Fact]
    public void DataDirInfo_ReportsDiskAndLog()
    {
        File.WriteAllText(Path.Combine(_dir, "node.log"), "12345");

        DataDirDiagnostics info = Diagnostics.DataDirInfo(_dir);

        Assert.True(info.Exists);
        Assert.Equal(5, info.NodeLogBytes);
        Assert.True(info.TotalBytes > 0);
        Assert.True(info.FreeBytes >= 0);
    }

    [Fact]
    public void DataDirInfo_ReportsDiskForAMissingDir()
    {
        DataDirDiagnostics info = Diagnostics.DataDirInfo(Path.Combine(_dir, "not", "yet"));

        Assert.False(info.Exists);
        Assert.Null(info.NodeLogBytes);
        Assert.True(info.TotalBytes > 0);
    }

    // ---- Access ----

    private static HttpRequest Request(IPAddress remote, string? session = null, string? forwardedFor = null)
    {
        DefaultHttpContext context = new();
        context.Connection.RemoteIpAddress = remote;
        if (session != null) context.Request.Headers.Authorization = $"Bearer {session}";
        if (forwardedFor != null) context.Request.Headers["X-Forwarded-For"] = forwardedFor;
        return context.Request;
    }

    [Fact]
    public void Allowed_OnTheNodeMachineWithoutSigningIn()
    {
        Assert.True(Diagnostics.Allowed(Request(IPAddress.Loopback)));
        Assert.True(Diagnostics.Allowed(Request(IPAddress.IPv6Loopback)));
        Assert.True(Diagnostics.Allowed(Request(IPAddress.Parse("::ffff:127.0.0.1"))));
    }

    [Fact]
    public void Allowed_NotThroughALocalReverseProxy() =>
        Assert.False(Diagnostics.Allowed(Request(IPAddress.Loopback, forwardedFor: "203.0.113.7")));

    [Fact]
    public void Allowed_FromElsewhereOnlyWithASession()
    {
        IPAddress lan = IPAddress.Parse("192.168.1.20");
        Auth.SetPassword("correct horse battery");

        Assert.False(Diagnostics.Allowed(Request(lan)));
        Assert.False(Diagnostics.Allowed(Request(lan, "not-a-session")));
        Assert.True(Diagnostics.Allowed(Request(lan, Auth.CreateSession())));
    }
}
