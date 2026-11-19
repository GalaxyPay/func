namespace FUNC.Tray;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        // One helper per signed-in user: autostart and installer relaunches can overlap.
        using var mutex = new Mutex(true, "FUNC.Tray", out bool first);
        if (!first) return 0;

        Notifier.Init();
#if WINDOWS
        return Windows.Win32Tray.Run();
#else
        return App.Run(args);
#endif
    }
}
