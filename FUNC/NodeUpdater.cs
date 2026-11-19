using System.Text.Json;
using FUNC.Controllers;
using FUNC.Models;

namespace FUNC
{
    // Keeps FUNC's own node binaries on the latest release, when the user turns it on.
    // Adopted nodes are skipped: their package manager or update.sh owns their binaries.
    public sealed class NodeUpdater(ILogger<NodeUpdater> logger) : BackgroundService
    {
        private static readonly string settingsPath = Path.Combine(Utils.appDataDir, "node-update.json");
        private static readonly TimeSpan FirstCheck = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(6);

        // Released when auto-update is turned on, so the check runs right away.
        private static readonly SemaphoreSlim wake = new(0, 1);

        private static bool enabled = LoadEnabled();
        private static DateTimeOffset? lastCheck;
        private static string? lastError;
        private static string? lastUpdated;

        public static AutoUpdate Status() => new()
        {
            Enabled = enabled,
            LastCheck = lastCheck,
            LastError = lastError,
            LastUpdated = lastUpdated,
        };

        public static void SetEnabled(bool value)
        {
            if (value == enabled) return;
            File.WriteAllText(settingsPath, JsonSerializer.Serialize(new { enabled = value }));
            enabled = value;
            if (value && wake.CurrentCount == 0) wake.Release();
        }

        private static bool LoadEnabled()
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(settingsPath));
                return doc.RootElement.GetProperty("enabled").GetBoolean();
            }
            catch { return false; }
        }

        protected override async Task ExecuteAsync(CancellationToken ct)
        {
            TimeSpan wait = FirstCheck;
            while (!ct.IsCancellationRequested)
            {
                try { await wake.WaitAsync(wait, ct); }
                catch (OperationCanceledException) { return; }
                if (enabled) await Check();
                wait = CheckInterval;
            }
        }

        private async Task Check()
        {
            try
            {
                if (External.Load() != null) return;
                string installed = await GoalController.InstalledVersion(Path.Combine(Utils.appDataDir, "bin"));
                // With nothing installed yet, FUNC's first run installs the latest anyway.
                if (installed.Length == 0) return;
                string latest = await GoalController.LatestVersion();
                if (IsNewer(latest, installed))
                {
                    logger.LogInformation("Auto-updating the node from {Installed} to {Latest}", installed, latest);
                    await GoalController.Install("latest");
                    lastUpdated = latest;
                }
                lastError = null;
            }
            catch (Exception ex)
            {
                logger.LogWarning("Node auto-update failed: {Message}", ex.Message);
                lastError = ex.Message;
            }
            lastCheck = DateTimeOffset.UtcNow;
        }

        // Versions look like "5.0.5". Anything unparseable is left alone.
        internal static bool IsNewer(string latest, string installed) =>
            Version.TryParse(latest, out var l) && Version.TryParse(installed, out var i) && l > i;
    }
}
