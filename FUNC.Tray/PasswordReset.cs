using System.ComponentModel;
using System.Diagnostics;

namespace FUNC.Tray;

// "Reset password…": runs `FUNC --reset-password` with the OS admin prompt (UAC on
// Windows, the authorization dialog on macOS), then opens FUNC at first-run setup.
// The service runs as SYSTEM/root, so only an admin can reset its password.
internal static class PasswordReset
{
    // The service executable sits next to the tray helper.
    private static readonly string FuncPath = Path.Combine(AppContext.BaseDirectory, OperatingSystem.IsWindows() ? "FUNC.exe" : "FUNC");

    public static void Start(string openUrl) => _ = Task.Run(() =>
    {
        try
        {
            if (Run()) Browser.Open(openUrl);
        }
        catch (Exception ex)
        {
            Log.Write($"Password reset failed: {ex.Message}");
        }
    });

    // True if the password was reset; false if the admin prompt was cancelled or it failed.
    private static bool Run()
    {
        ProcessStartInfo psi;
        if (OperatingSystem.IsWindows())
        {
            // The signed FUNC.exe itself, elevated. Not through PowerShell, which
            // Defender has flagged before.
            psi = new ProcessStartInfo(FuncPath, "--reset-password")
            {
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Hidden,
            };
        }
        else
        {
            string path = FuncPath.Replace("\\", "\\\\").Replace("\"", "\\\"");
            string script = $"do shell script quoted form of \"{path}\" & \" --reset-password\" " +
                "with prompt \"FUNC needs an administrator to reset its password.\" with administrator privileges";
            psi = new ProcessStartInfo("osascript", ["-e", script]) { UseShellExecute = false };
        }

        try
        {
            using Process? process = Process.Start(psi);
            if (process == null) return false;
            process.WaitForExit();
            if (process.ExitCode != 0) Log.Write($"Password reset exited with {process.ExitCode}");
            return process.ExitCode == 0;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223) // ERROR_CANCELLED: UAC declined
        {
            return false;
        }
    }
}
