using System.Text.Json;
using FUNC.Models;
using static System.OperatingSystem;

namespace FUNC
{
    // Which optional desktop notifications the tray / menu-bar helper raises. Kept by the
    // service so the dashboard can change it and every user's helper picks it up.
    public static class Notifications
    {
        private static readonly string settingsPath = Path.Combine(Utils.appDataDir, "notifications.json");
        private static NotificationSettings settings = Load();

        // Whether a helper exists on this platform to show them at all.
        public static bool TrayAvailable => IsWindows() || IsMacOS();

        public static bool Blocks => settings.Blocks;

        public static NotificationSettings Get() => new() { Blocks = settings.Blocks, Tray = TrayAvailable };

        public static NotificationSettings Set(NotificationSettings value)
        {
            NotificationSettings next = new() { Blocks = value.Blocks };
            File.WriteAllText(settingsPath, JsonSerializer.Serialize(next, NotificationSettings.Json));
            settings = next;
            return Get();
        }

        private static NotificationSettings Load()
        {
            try { return Parse(File.ReadAllText(settingsPath)); }
            catch { return new(); }
        }

        // {"blocks":true}. Anything missing is off.
        internal static NotificationSettings Parse(string json) =>
            JsonSerializer.Deserialize<NotificationSettings>(json, NotificationSettings.Json) ?? new();
    }
}
