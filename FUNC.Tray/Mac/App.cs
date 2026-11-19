using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;

namespace FUNC.Tray;

// macOS: Avalonia maps TrayIcon/NativeMenu onto NSStatusItem/NSMenu, so the
// menu-bar extra is fully native. No windows.
internal sealed class App : Application
{
    private TrayModel? model;

    public override void Initialize() => Name = "FUNC";

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var ui = new AvaloniaTray(this);
            model = new TrayModel(ui, () => Dispatcher.UIThread.Post(() => desktop.Shutdown()));
            desktop.Exit += (_, _) =>
            {
                model.Dispose();
                ui.Dispose();
            };
            model.Start();
        }
        base.OnFrameworkInitializationCompleted();
    }

    public static int Run(string[] args) =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            // A menu-bar extra, not a regular app: no Dock icon or app menu.
            .With(new MacOSPlatformOptions { ShowInDock = false, DisableDefaultApplicationMenuItems = true })
            .LogToTrace()
            .StartWithClassicDesktopLifetime(args, ShutdownMode.OnExplicitShutdown);
}

internal sealed class AvaloniaTray : ITrayUi, IDisposable
{
    private readonly TrayIcon icon;

    public AvaloniaTray(Application app)
    {
        // ToolTipText must be non-null before SetIcons: Avalonia.Native's Attach() marshals it
        // to UTF-8 and throws ArgumentNullException on null, crashing the app at startup.
        icon = new TrayIcon { Menu = new NativeMenu(), ToolTipText = "FUNC", IsVisible = true };
        MacOSProperties.SetIsTemplateIcon(icon, true);
        TrayIcon.SetIcons(app, [icon]);
    }

    public void Update(TrayView view) => Dispatcher.UIThread.Post(() =>
    {
        icon.Icon = Icons.Get(view.Look);
        icon.ToolTipText = view.Tooltip;
        // Rebuild the items of the menu attached at startup. Assigning a new NativeMenu to
        // icon.Menu makes Avalonia.Native throw "The menu being updated does not match".
        NativeMenu menu = icon.Menu!;
        menu.Items.Clear();
        foreach (MenuEntry entry in view.Menu)
        {
            if (entry.IsSeparator)
            {
                menu.Items.Add(new NativeMenuItemSeparator());
                continue;
            }
            var item = new NativeMenuItem(entry.Text) { IsEnabled = entry.Click != null };
            if (entry.Click is Action click) item.Click += (_, _) => click();
            menu.Items.Add(item);
        }
    });

    public void Dispose()
    {
        icon.IsVisible = false;
        icon.Dispose();
    }
}
