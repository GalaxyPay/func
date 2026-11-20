using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FUNC.Tray;

internal enum TrayLook { Normal, Alert, Offline }

// One menu line. No Click means a disabled, informational line.
internal sealed record MenuEntry(string Text, Action? Click = null, bool IsDefault = false)
{
    public static readonly MenuEntry Separator = new("-");
    public bool IsSeparator => ReferenceEquals(this, Separator);
}

internal sealed record TrayView(TrayLook Look, string Tooltip, IReadOnlyList<MenuEntry> Menu);

// The platform's icon and menu (Win32 on Windows, Avalonia on macOS).
// Update may be called from any thread.
internal interface ITrayUi
{
    void Update(TrayView view);
}

// Polls the local FUNC service, raises notifications and describes what the
// icon and menu should show. Platform-neutral; the ITrayUi draws it.
// No Quit item: the tray is FUNC's face on the desktop and lives as long as FUNC
// is installed.
internal sealed class TrayModel(ITrayUi ui) : IDisposable
{
    public const string BaseUrl = "http://localhost:3536/";
    private const string MessagesUrl = BaseUrl + "?open=messages";
    private const string SettingsUrl = BaseUrl + "?open=settings";
    // FUNC_TRAY_URL points the helper at another server, for testing against a stub.
    private static readonly Uri StatusUri = new(Environment.GetEnvironmentVariable("FUNC_TRAY_URL") ?? "http://127.0.0.1:3536/func/tray");
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(60);

    private readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(30) };
    private readonly CancellationTokenSource cts = new();
    private readonly TrayState state = TrayState.Load();
    private readonly Lock gate = new();
    private Status? last;
    private bool polled;

    public void Start()
    {
        ui.Update(BuildView());
        _ = Loop();
    }

    public static void OpenDashboard() => Browser.Open(BaseUrl);

    private static readonly MenuEntry SettingsEntry = new("Settings", () => Browser.Open(SettingsUrl));
    private static readonly MenuEntry ResetPasswordEntry = new("Reset Password…", () => PasswordReset.Start(BaseUrl));

    private async Task Loop()
    {
        while (!cts.IsCancellationRequested)
        {
            Status? status = null;
            try { status = await http.GetFromJsonAsync(StatusUri, TrayJson.Default.Status, cts.Token); }
            catch (OperationCanceledException) when (cts.IsCancellationRequested) { return; }
            // Logged once per outage, not on every poll.
            catch (Exception ex) { if (last != null || !polled) Log.Write($"FUNC status: {ex.Message}"); }

            TrayView view;
            lock (gate)
            {
                polled = true;
                last = status;
                if (status != null) Notify(status);
                view = BuildView();
            }
            ui.Update(view);

            try { await Task.Delay(PollInterval, cts.Token); }
            catch (OperationCanceledException) { return; }
        }
    }

    // Raises one OS notification per new message and per new update version.
    private void Notify(Status status)
    {
        HashSet<ulong> ids = status.Messages.Select(m => m.Id).ToHashSet();
        if (!state.Initialized && ids.Count > 0)
        {
            // Don't announce the whole backlog on first run; those still count as unseen.
            // Waits for a non-empty list, which may only be the service not having read it yet.
            state.NotifiedMessageIds = [.. ids];
            state.Initialized = true;
        }

        List<Message> fresh = status.Messages.Where(m => !state.NotifiedMessageIds.Contains(m.Id)).ToList();
        if (fresh.Count == 1)
            Notifier.Show("New FUNC message", fresh[0].Title, MessagesUrl);
        else if (fresh.Count > 1)
            Notifier.Show($"{fresh.Count} new FUNC messages", string.Join("\n", fresh.Select(m => m.Title)), MessagesUrl);

        // Forget ids for messages that were removed. An empty list usually means the service
        // couldn't read them yet (node not running after a restart), not that all were
        // removed; forgetting then would re-announce every message on the next poll.
        if (ids.Count > 0)
        {
            state.NotifiedMessageIds = [.. ids];
            state.SeenMessageIds.IntersectWith(ids);
        }

        if (status.FuncUpdate is { Available: true, Latest: string funcLatest } && funcLatest != state.NotifiedFuncVersion)
        {
            Notifier.Show("FUNC update available", $"Version {funcLatest} is ready to install.", SettingsUrl);
            state.NotifiedFuncVersion = funcLatest;
        }
        if (status.NodeUpdate is { Available: true, Notify: true, Latest: string nodeLatest } && nodeLatest != state.NotifiedNodeVersion)
        {
            Notifier.Show("Node update available", $"Algorand node {nodeLatest} is ready to install.", SettingsUrl);
            state.NotifiedNodeVersion = nodeLatest;
        }
        // Alerted whatever the update mode: the node stalls at the upgrade round otherwise.
        if (status.NodeUpdate?.Unsupported is string unsupported && unsupported != state.NotifiedUnsupported)
        {
            Notifier.Show("Node update required", $"{unsupported}. Update the node or it will stall.", SettingsUrl);
            state.NotifiedUnsupported = unsupported;
        }

        NotifyBlocks(status);
        NotifyExpiringKeys(status);

        state.Save();
    }

    // One notification per poll for the blocks this node created since the last one,
    // when the user turned them on. Blocks seen while they're off are skipped, not saved up.
    private void NotifyBlocks(Status status)
    {
        List<BlockWin> blocks = status.Blocks ?? [];
        if (state.NotifiedBlockRound is long seen && status.NotifyBlocks)
        {
            List<BlockWin> fresh = blocks.Where(b => b.Round > seen).OrderBy(b => b.Round).ToList();
            if (fresh.Count == 1)
                Notifier.Show("🎉 Block created", $"This node created block {Round(fresh[0].Round)}.", BaseUrl);
            else if (fresh.Count > 1)
                Notifier.Show($"🎉 {fresh.Count} blocks created", $"This node created blocks {string.Join(", ", fresh.Select(b => Round(b.Round)))}.", BaseUrl);
        }
        // Follows the store even when its rounds go down (its history file was reset).
        if (blocks.Count > 0) state.NotifiedBlockRound = blocks.Max(b => b.Round);
        else state.NotifiedBlockRound ??= 0;
    }

    // One alert per key that's due to expire; forgotten once it's renewed or gone.
    private void NotifyExpiringKeys(Status status)
    {
        List<ExpiringKey> keys = status.ExpiringKeys ?? [];
        List<ExpiringKey> fresh = keys.Where(k => !state.NotifiedExpiringKeyIds.Contains(k.Id)).ToList();
        if (fresh.Count == 1)
            Notifier.Show("Participation key expiring", $"The key for {ShortAddress(fresh[0].Address)} {ExpiresText(fresh[0].Expires)}. Generate and register a new one to stay online.", BaseUrl);
        else if (fresh.Count > 1)
            Notifier.Show($"{fresh.Count} participation keys expiring", $"Keys for {string.Join(", ", fresh.Select(k => ShortAddress(k.Address)))} expire within a week. Generate and register new ones to stay online.", BaseUrl);
        state.NotifiedExpiringKeyIds = [.. keys.Select(k => k.Id)];
    }

    internal static string ShortAddress(string address) => address.Length > 10 ? $"{address[..5]}…{address[^5..]}" : address;

    internal static string ExpiresText(DateTimeOffset? expires) =>
        expires is not DateTimeOffset at ? "expires soon"
        : at <= DateTimeOffset.Now ? "has expired"
        : $"expires around {at.ToLocalTime().ToString("MMM d", CultureInfo.CurrentCulture)}";

    private static string Round(long round) => round.ToString("N0", CultureInfo.InvariantCulture);

    private TrayView BuildView()
    {
        List<MenuEntry> menu = [];
        Status? status = last;
        if (status == null)
        {
            menu.Add(new(polled ? "FUNC is not responding" : "Checking FUNC…"));
            menu.Add(MenuEntry.Separator);
            menu.Add(new("Open FUNC", OpenDashboard, IsDefault: true));
            menu.Add(MenuEntry.Separator);
            menu.Add(SettingsEntry);
            menu.Add(ResetPasswordEntry);
            return new(polled ? TrayLook.Offline : TrayLook.Normal, polled ? "FUNC is not responding" : "FUNC", menu);
        }

        int unseen = status.Messages.Count(m => !state.SeenMessageIds.Contains(m.Id));
        bool funcUpdate = status.FuncUpdate?.Available == true;
        bool nodeUpdate = status.NodeUpdate is { Available: true, Notify: true };
        bool nodeUnsupported = status.NodeUpdate?.Unsupported != null;
        List<ExpiringKey> expiring = status.ExpiringKeys ?? [];

        menu.Add(new($"FUNC {(status.Version != null ? "v" + status.Version + " " : "")}is running"));
        menu.Add(new($"Node: {status.NodeStatus ?? "Unknown"}"));
        menu.Add(MenuEntry.Separator);
        menu.Add(new("Open FUNC", OpenDashboard, IsDefault: true));
        if (status.Messages.Count > 0)
            menu.Add(new(unseen > 0 ? $"Messages ({unseen} new)" : "Messages", OpenMessages));
        if (funcUpdate)
            menu.Add(new($"FUNC update available: v{status.FuncUpdate!.Latest}", () => Browser.Open(SettingsUrl)));
        if (nodeUnsupported)
            menu.Add(new("Node update required", () => Browser.Open(SettingsUrl)));
        else if (nodeUpdate)
            menu.Add(new($"Node update available: v{status.NodeUpdate!.Latest}", () => Browser.Open(SettingsUrl)));
        foreach (ExpiringKey key in expiring)
            menu.Add(new($"Key for {ShortAddress(key.Address)} {ExpiresText(key.Expires)}", OpenDashboard));
        menu.Add(MenuEntry.Separator);
        menu.Add(SettingsEntry);
        menu.Add(ResetPasswordEntry);

        List<string> alerts = [];
        if (unseen > 0) alerts.Add(unseen == 1 ? "1 new message" : $"{unseen} new messages");
        if (funcUpdate) alerts.Add("FUNC update available");
        if (nodeUnsupported) alerts.Add("Node update required");
        else if (nodeUpdate) alerts.Add("Node update available");
        if (expiring.Count > 0) alerts.Add(expiring.Count == 1 ? "Participation key expiring" : $"{expiring.Count} participation keys expiring");

        return new(
            alerts.Count > 0 ? TrayLook.Alert : TrayLook.Normal,
            alerts.Count > 0 ? $"FUNC — {string.Join(", ", alerts)}" : "FUNC is running",
            menu);
    }

    private void OpenMessages()
    {
        TrayView view;
        lock (gate)
        {
            if (last != null)
            {
                state.SeenMessageIds.UnionWith(last.Messages.Select(m => m.Id));
                state.Save();
            }
            view = BuildView();
        }
        ui.Update(view);
        Browser.Open(MessagesUrl);
    }

    public void Dispose()
    {
        cts.Cancel();
        http.Dispose();
    }
}

// Mirrors FUNC.Models.TrayStatus (GET func/tray).
internal sealed record Status(string? Version, string? NodeStatus, Update? FuncUpdate, Update? NodeUpdate, List<Message> Messages, bool NotifyBlocks = false, List<BlockWin>? Blocks = null, List<ExpiringKey>? ExpiringKeys = null);
internal sealed record Update(string? Installed, string? Latest, bool Available, bool Notify, string? Unsupported);
internal sealed record Message(ulong Id, string Title);
internal sealed record BlockWin(long Round, DateTimeOffset Time);
internal sealed record ExpiringKey(string Id, string Address, DateTimeOffset? Expires);

// Source-generated JSON: the trimmed / native AOT builds have no reflection serializer.
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web, WriteIndented = true)]
[JsonSerializable(typeof(Status))]
[JsonSerializable(typeof(TrayState))]
internal sealed partial class TrayJson : JsonSerializerContext;
