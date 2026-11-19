using System.Text.Json;

namespace FUNC.Tray;

// What this user has already been told about, so each message or update
// notifies once. Kept per user, separate from the dashboard's read state.
internal sealed class TrayState
{
    public bool Initialized { get; set; }
    public HashSet<ulong> SeenMessageIds { get; set; } = [];
    public HashSet<ulong> NotifiedMessageIds { get; set; } = [];
    public string? NotifiedFuncVersion { get; set; }
    public string? NotifiedNodeVersion { get; set; }
    // The installed node's consensus problem last alerted, so each one notifies once.
    public string? NotifiedUnsupported { get; set; }

    // %LOCALAPPDATA%\FUNC on Windows, ~/Library/Application Support/FUNC on macOS.
    private static string FilePath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FUNC", "tray.json");

    public static TrayState Load()
    {
        try { return JsonSerializer.Deserialize(File.ReadAllText(FilePath), TrayJson.Default.TrayState) ?? new(); }
        catch { return new(); }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, TrayJson.Default.TrayState));
        }
        catch { }
    }
}
