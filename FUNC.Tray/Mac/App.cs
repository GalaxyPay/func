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
        icon = new TrayIcon { Menu = new NativeMenu(), IsVisible = true };
        MacOSProperties.SetIsTemplateIcon(icon, true);
        TrayIcon.SetIcons(app, [icon]);
    }

    public void Update(TrayView view) => Dispatcher.UIThread.Post(() =>
    {
        icon.Icon = Icons.Get(view.Look);
        icon.ToolTipText = view.Tooltip;
        var menu = new NativeMenu();
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
        icon.Menu = menu;
    });

    public void Dispose()
    {
        icon.IsVisible = false;
        icon.Dispose();
    }
}
