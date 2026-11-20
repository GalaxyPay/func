using System.Reflection;
using System.Runtime.InteropServices;
using static FUNC.Tray.Windows.Native;

namespace FUNC.Tray.Windows;

// Windows notification-area icon on plain Win32: Shell_NotifyIcon for the icon and
// TrackPopupMenuEx for the standard system menu (themed, dark mode, keyboard, DPI).
internal sealed unsafe class Win32Tray : ITrayUi
{
    private const uint IconId = 1;
    private const uint WM_TRAY = WM_APP + 1;     // icon callbacks
    private const uint WM_REFRESH = WM_APP + 2;  // a new view is waiting
    private static readonly uint WM_TASKBARCREATED = RegisterWindowMessage("TaskbarCreated");

    private static Win32Tray? instance;

    private readonly Lock gate = new();
    private readonly Dictionary<TrayLook, nint> icons = [];
    private TrayView? pending;
    private TrayView? shown;
    private nint hwnd;
    private bool added;

    public static int Run()
    {
        AllowDarkMenus();
        var tray = new Win32Tray();
        instance = tray;
        tray.CreateWindow();
        using var model = new TrayModel(tray);
        model.Start();

        MSG msg;
        while (GetMessage(&msg, 0, 0, 0) > 0)
        {
            TranslateMessage(&msg);
            DispatchMessage(&msg);
        }
        foreach (nint icon in tray.icons.Values) DestroyIcon(icon);
        return 0;
    }

    // Any thread: hand the view to the window's thread.
    public void Update(TrayView view)
    {
        lock (gate) pending = view;
        PostMessage(hwnd, WM_REFRESH, 0, 0);
    }

    private void CreateWindow()
    {
        nint instanceHandle = GetModuleHandle(null);
        fixed (char* className = "FUNC.Tray")
        {
            var wc = new WNDCLASSEXW
            {
                cbSize = (uint)sizeof(WNDCLASSEXW),
                lpfnWndProc = &WndProc,
                hInstance = instanceHandle,
                lpszClassName = className,
            };
            if (RegisterClassEx(&wc) == 0) throw new InvalidOperationException($"RegisterClassEx failed: {Marshal.GetLastPInvokeError()}");
        }
        // A hidden top-level window (not message-only) so it hears TaskbarCreated
        // and can re-add the icon after Explorer restarts.
        hwnd = CreateWindowEx(0, "FUNC.Tray", "FUNC Tray", 0, 0, 0, 0, 0, 0, 0, instanceHandle, 0);
        if (hwnd == 0) throw new InvalidOperationException($"CreateWindowEx failed: {Marshal.GetLastPInvokeError()}");
    }

    [UnmanagedCallersOnly]
    private static nint WndProc(nint hwnd, uint msg, nuint wParam, nint lParam)
    {
        try
        {
            if (instance?.Handle(hwnd, msg, wParam, lParam) is nint result) return result;
        }
        catch (Exception ex)
        {
            Log.Write($"Tray window: {ex}");
        }
        return DefWindowProc(hwnd, msg, wParam, lParam);
    }

    private nint? Handle(nint window, uint msg, nuint wParam, nint lParam)
    {
        if (msg == WM_REFRESH)
        {
            TrayView? view;
            lock (gate) view = pending;
            if (view != null) Apply(view);
            return 0;
        }
        if (msg == WM_TRAY)
        {
            uint ev = (uint)(lParam & 0xFFFF);
            // Version 4 puts the anchor point (screen coordinates) in wParam.
            int x = (short)(wParam & 0xFFFF), y = (short)((wParam >> 16) & 0xFFFF);
            if (ev == WM_CONTEXTMENU) ShowMenu(x, y);
            else if (ev is NIN_SELECT or NIN_KEYSELECT) TrayModel.OpenDashboard();
            return 0;
        }
        if (msg == WM_TASKBARCREATED && WM_TASKBARCREATED != 0)
        {
            added = false;
            if (shown != null) Apply(shown);
            return 0;
        }
        if (msg == WM_SETTINGCHANGE && lParam != 0 && new string((char*)lParam) == "ImmersiveColorSet")
        {
            // Light/dark switched: rebuild menu theming for the next popup.
            RefreshMenuTheme();
            return null;
        }
        if (msg == WM_CLOSE)
        {
            DestroyWindow(window);
            return 0;
        }
        if (msg == WM_DESTROY)
        {
            if (added)
            {
                NOTIFYICONDATAW data = NewData();
                Shell_NotifyIcon(NIM_DELETE, &data);
                added = false;
            }
            PostQuitMessage(0);
            return 0;
        }
        return null;
    }

    private NOTIFYICONDATAW NewData() => new()
    {
        cbSize = (uint)sizeof(NOTIFYICONDATAW),
        hWnd = hwnd,
        uID = IconId,
    };

    private void Apply(TrayView view)
    {
        shown = view;
        NOTIFYICONDATAW data = NewData();
        data.uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP | NIF_SHOWTIP;
        data.uCallbackMessage = WM_TRAY;
        data.hIcon = Icon(view.Look);
        CopyString(view.Tooltip, data.szTip, 128);

        if (added)
        {
            Shell_NotifyIcon(NIM_MODIFY, &data);
            return;
        }
        // Fails while Explorer is starting; TaskbarCreated will bring us back.
        if (!Shell_NotifyIcon(NIM_ADD, &data)) return;
        data.uVersion = NOTIFYICON_VERSION_4;
        Shell_NotifyIcon(NIM_SETVERSION, &data);
        added = true;
    }

    private void ShowMenu(int x, int y)
    {
        IReadOnlyList<MenuEntry> entries = shown?.Menu ?? [];
        nint menu = CreatePopupMenu();
        try
        {
            for (int i = 0; i < entries.Count; i++)
            {
                MenuEntry entry = entries[i];
                if (entry.IsSeparator)
                {
                    AppendMenu(menu, MF_SEPARATOR, 0, null);
                    continue;
                }
                // Menu text treats & as a mnemonic marker.
                AppendMenu(menu, MF_STRING | (entry.Click == null ? MF_GRAYED : 0), (nuint)(i + 1), entry.Text.Replace("&", "&&"));
                if (entry.IsDefault) SetMenuDefaultItem(menu, (uint)(i + 1), 0);
            }

            // Required so the menu closes when the user clicks elsewhere.
            SetForegroundWindow(hwnd);
            int command = TrackPopupMenuEx(menu, TPM_RIGHTBUTTON | TPM_RETURNCMD | TPM_NONOTIFY | TPM_BOTTOMALIGN, x, y, hwnd, 0);
            PostMessage(hwnd, WM_NULL, 0, 0);

            if (command > 0 && command <= entries.Count) entries[command - 1].Click?.Invoke();
        }
        finally
        {
            DestroyMenu(menu);
        }
    }

    private nint Icon(TrayLook look)
    {
        if (icons.TryGetValue(look, out nint icon)) return icon;
        string name = look switch
        {
            TrayLook.Alert => "tray-alert.ico",
            TrayLook.Offline => "tray-offline.ico",
            _ => "tray.ico",
        };
        icon = LoadIcon(name, GetSystemMetrics(SM_CXSMICON), GetSystemMetrics(SM_CYSMICON));
        icons[look] = icon;
        return icon;
    }

    // Picks the best image in an embedded .ico for the small-icon size: the
    // smallest one at least that big, else the largest.
    private static nint LoadIcon(string name, int cx, int cy)
    {
        using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Missing resource {name}");
        byte[] ico = new byte[stream.Length];
        stream.ReadExactly(ico);

        int count = BitConverter.ToUInt16(ico, 4);
        int best = -1, bestWidth = 0;
        for (int i = 0; i < count; i++)
        {
            int width = ico[6 + 16 * i];
            if (width == 0) width = 256;
            bool better = best < 0
                || (width >= cx && (bestWidth < cx || width < bestWidth))
                || (bestWidth < cx && width > bestWidth);
            if (better) { best = i; bestWidth = width; }
        }
        int entry = 6 + 16 * best;
        uint size = BitConverter.ToUInt32(ico, entry + 8);
        int offset = (int)BitConverter.ToUInt32(ico, entry + 12);
        fixed (byte* bits = &ico[offset])
        {
            return CreateIconFromResourceEx(bits, size, true, 0x00030000, cx, cy, LR_DEFAULTCOLOR);
        }
    }

    private static void CopyString(string text, char* buffer, int capacity)
    {
        int length = Math.Min(text.Length, capacity - 1);
        text.AsSpan(0, length).CopyTo(new Span<char>(buffer, capacity));
        buffer[length] = '\0';
    }
}
