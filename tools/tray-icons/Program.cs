using SkiaSharp;
using Svg.Skia;

// Regenerates the Windows tray icons from the logo:
//   dotnet run --project tools/tray-icons -- assets/icon.svg FUNC.Tray/Windows
// Writes tray.ico, tray-alert.ico and tray-offline.ico.
var svg = new SKSvg();
svg.Load(args[0]);
var pic = svg.Picture!;
var b = pic.CullRect;
int[] sizes = [16, 20, 24, 32, 40, 48, 64];

foreach (var look in new[] { "", "-alert", "-offline" })
{
    var pngs = new List<(int size, byte[] data)>();
    foreach (int size in sizes)
    {
        using var bmp = new SKBitmap(new SKImageInfo(size, size, SKColorType.Bgra8888, SKAlphaType.Premul));
        using (var c = new SKCanvas(bmp))
        {
            c.Clear(SKColors.Transparent);
            float scale = size / Math.Max(b.Width, b.Height);
            c.Save();
            c.Scale(scale);
            c.Translate(-b.Left, -b.Top);
            using var paint = new SKPaint { Color = SKColors.White.WithAlpha(look == "-offline" ? (byte)110 : (byte)255) };
            c.DrawPicture(pic, paint);
            c.Restore();
            if (look == "-alert")
            {
                float r = size * 0.24f;
                var center = new SKPoint(size - r, size - r);
                using var fill = new SKPaint { Color = SKColor.Parse("#F59E0B"), IsAntialias = true };
                using var ring = new SKPaint { Color = SKColors.White, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = Math.Max(1, size * 0.06f) };
                c.DrawCircle(center, r, fill);
                c.DrawCircle(center, r, ring);
            }
        }
        using var img = SKImage.FromBitmap(bmp);
        pngs.Add((size, img.Encode(SKEncodedImageFormat.Png, 100).ToArray()));
    }

    using var fs = File.Create(Path.Combine(args[1], $"tray{look}.ico"));
    using var w = new BinaryWriter(fs);
    w.Write((ushort)0); w.Write((ushort)1); w.Write((ushort)pngs.Count);
    int offset = 6 + 16 * pngs.Count;
    foreach (var (size, data) in pngs)
    {
        w.Write((byte)(size >= 256 ? 0 : size)); w.Write((byte)(size >= 256 ? 0 : size));
        w.Write((byte)0); w.Write((byte)0); w.Write((ushort)1); w.Write((ushort)32);
        w.Write((uint)data.Length); w.Write((uint)offset);
        offset += data.Length;
    }
    foreach (var (_, data) in pngs) w.Write(data);
}
Console.WriteLine("ok");
