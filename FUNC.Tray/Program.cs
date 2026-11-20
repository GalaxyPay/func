namespace FUNC.Tray;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        // A startup crash would otherwise leave no trace (launchd/Task Scheduler discard output).
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Log.Write($"Crash: {e.ExceptionObject}");

        // One helper per signed-in user: autostart and installer relaunches can overlap.
        using var mutex = new Mutex(true, "FUNC.Tray", out bool first);
        if (!first)
        {
            Log.Write("Already running for this user; exiting.");
            return 0;
        }

        Notifier.Init();
#if WINDOWS
        return Windows.Win32Tray.Run();
#else
        return App.Run(args);
#endif
    }
}
