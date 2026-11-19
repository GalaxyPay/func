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
internal sealed class TrayModel(ITrayUi ui, Action quit) : IDisposable
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
        if (!state.Initialized)
        {
            // Don't announce the whole backlog on first run; those still count as unseen.
            state.NotifiedMessageIds = [.. ids];
            state.Initialized = true;
        }

        List<Message> fresh = status.Messages.Where(m => !state.NotifiedMessageIds.Contains(m.Id)).ToList();
        if (fresh.Count == 1)
            Notifier.Show("New FUNC message", fresh[0].Title, MessagesUrl);
        else if (fresh.Count > 1)
            Notifier.Show($"{fresh.Count} new FUNC messages", string.Join("\n", fresh.Select(m => m.Title)), MessagesUrl);

        // Forget ids for messages that were removed.
        state.NotifiedMessageIds = [.. ids];
        state.SeenMessageIds.IntersectWith(ids);

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

        state.Save();
    }

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
            menu.Add(new("Quit FUNC Tray", quit));
            return new(polled ? TrayLook.Offline : TrayLook.Normal, polled ? "FUNC is not responding" : "FUNC", menu);
        }

        int unseen = status.Messages.Count(m => !state.SeenMessageIds.Contains(m.Id));
        bool funcUpdate = status.FuncUpdate?.Available == true;
        bool nodeUpdate = status.NodeUpdate is { Available: true, Notify: true };
        bool nodeUnsupported = status.NodeUpdate?.Unsupported != null;

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
        menu.Add(MenuEntry.Separator);
        menu.Add(new("Quit FUNC Tray", quit));

        List<string> alerts = [];
        if (unseen > 0) alerts.Add(unseen == 1 ? "1 new message" : $"{unseen} new messages");
        if (funcUpdate) alerts.Add("FUNC update available");
        if (nodeUnsupported) alerts.Add("Node update required");
        else if (nodeUpdate) alerts.Add("Node update available");

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
internal sealed record Status(string? Version, string? NodeStatus, Update? FuncUpdate, Update? NodeUpdate, List<Message> Messages);
internal sealed record Update(string? Installed, string? Latest, bool Available, bool Notify, string? Unsupported);
internal sealed record Message(ulong Id, string Title);

// Source-generated JSON: the trimmed / native AOT builds have no reflection serializer.
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web, WriteIndented = true)]
[JsonSerializable(typeof(Status))]
[JsonSerializable(typeof(TrayState))]
internal sealed partial class TrayJson : JsonSerializerContext;
