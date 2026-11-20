using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace FUNC.Tray;

// Menu-bar template icons drawn from the FUNC logo: black shapes the menu bar
// recolors to match light/dark mode. The alert dot is part of the template.
internal static class Icons
{
    private static readonly Dictionary<TrayLook, WindowIcon> cache = [];
    private static readonly Uri LogoUri = new("avares://FUNC.Tray/Assets/icon.png");

    public static WindowIcon Get(TrayLook look)
    {
        if (!cache.TryGetValue(look, out WindowIcon? icon))
        {
            icon = new WindowIcon(Render(look));
            cache[look] = icon;
        }
        return icon;
    }

    private static Bitmap Render(TrayLook look)
    {
        using var logo = new Bitmap(AssetLoader.Open(LogoUri));
        const int size = 36; // 18pt at 2x
        var rect = new Rect(0, 0, size, size);
        var target = new RenderTargetBitmap(new PixelSize(size, size));
        using (var ctx = target.CreateDrawingContext())
        {
            using (ctx.PushOpacity(look == TrayLook.Offline ? 0.4 : 1))
            using (ctx.PushOpacityMask(new ImageBrush(logo) { Stretch = Stretch.Uniform }, rect))
            {
                ctx.FillRectangle(Brushes.Black, rect);
            }

            if (look == TrayLook.Alert)
            {
                double r = size * 0.22;
                ctx.DrawEllipse(Brushes.Black, null, new Point(size - r, size - r), r, r);
            }
        }
        return target;
    }
}
