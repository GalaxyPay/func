using System.Diagnostics;
using System.Security;
#if WINDOWS
using System.Reflection;
using Microsoft.Win32;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;
#endif

namespace FUNC.Tray;

// Native OS notifications. Clicking one opens `url` where the platform allows.
internal static class Notifier
{
#if WINDOWS
    // An unpackaged app needs its own AppUserModelID registered for toasts to show.
    private const string Aumid = "GalaxyPay.FUNC.Tray";
    private const string AumidKey = @"Software\Classes\AppUserModelId\" + Aumid;
    private static ToastNotifier? notifier;

    // The logo as a file, for the toast app identity (IconUri needs a path).
    private static string? SaveLogo()
    {
        try
        {
            string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FUNC", "func.png");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using Stream source = Assembly.GetExecutingAssembly().GetManifestResourceStream("icon.png")!;
            using FileStream file = File.Create(path);
            source.CopyTo(file);
            return path;
        }
        catch { return null; }
    }
#endif

    public static void Init()
    {
#if WINDOWS
        try
        {
            using (var key = Registry.CurrentUser.CreateSubKey(AumidKey))
            {
                key.SetValue("DisplayName", "FUNC");
                string? iconPath = SaveLogo();
                if (iconPath != null) key.SetValue("IconUri", iconPath);
            }
            FUNC.Tray.Windows.Native.SetCurrentProcessExplicitAppUserModelID(Aumid);
            notifier = ToastNotificationManager.CreateToastNotifier(Aumid);
        }
        catch (Exception ex)
        {
            Log.Write($"Notifications unavailable: {ex.Message}");
        }
#endif
    }

    public static void Show(string title, string body, string url)
    {
        try
        {
#if WINDOWS
            if (notifier == null) return;
            var xml = new XmlDocument();
            xml.LoadXml(
                $"<toast launch=\"{SecurityElement.Escape(url)}\"><visual><binding template=\"ToastGeneric\">" +
                $"<text>{SecurityElement.Escape(title)}</text><text>{SecurityElement.Escape(body)}</text>" +
                "</binding></visual></toast>");
            var toast = new ToastNotification(xml);
            // Clicks reach us while the tray is running, which is whenever it can notify.
            toast.Activated += (_, _) => Browser.Open(url);
            notifier.Show(toast);
#else
            if (OperatingSystem.IsMacOS())
            {
                // osascript has no click action; the menu-bar item is the way back in.
                var psi = new ProcessStartInfo("osascript") { UseShellExecute = false };
                psi.ArgumentList.Add("-e");
                psi.ArgumentList.Add($"display notification {AppleString(body)} with title {AppleString(title)}");
                Process.Start(psi)?.Dispose();
            }
#endif
        }
        catch (Exception ex)
        {
            Log.Write($"Notification failed: {ex.Message}");
        }
    }

    private static string AppleString(string s) =>
        "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
}

internal static class Browser
{
    public static void Open(string url)
    {
        try
        {
            if (OperatingSystem.IsMacOS()) Process.Start("open", url)?.Dispose();
            else Process.Start(new ProcessStartInfo(url) { UseShellExecute = true })?.Dispose();
        }
        catch (Exception ex)
        {
            Log.Write($"Open {url} failed: {ex.Message}");
        }
    }
}
