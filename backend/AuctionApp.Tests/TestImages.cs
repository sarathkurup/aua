using SkiaSharp;

namespace AuctionApp.Tests;

/// <summary>
/// Builds images whose content says where it came from.
///
/// Each one is painted in three bands of flat colour. After the server has cropped and
/// re-encoded it, sampling the output tells you exactly which part of the original survived —
/// which is the only way to assert that a portrait was cropped towards the top rather than
/// through the middle.
/// </summary>
public static class TestImages
{
    public static readonly SKColor Band1 = new(220, 30, 30);   // red
    public static readonly SKColor Band2 = new(30, 200, 60);   // green
    public static readonly SKColor Band3 = new(40, 60, 230);   // blue

    /// <summary>Three horizontal bands, top to bottom. Taller than it is wide.</summary>
    public static byte[] Portrait(int width = 400, int height = 800) =>
        Banded(width, height, horizontal: true);

    /// <summary>Three vertical bands, left to right. Wider than it is tall.</summary>
    public static byte[] Landscape(int width = 800, int height = 400) =>
        Banded(width, height, horizontal: false);

    public static byte[] Square(int side = 500) => Banded(side, side, horizontal: true);

    private static byte[] Banded(int width, int height, bool horizontal)
    {
        using var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Black);

        var colours = new[] { Band1, Band2, Band3 };

        for (var band = 0; band < 3; band++)
        {
            using var paint = new SKPaint { Color = colours[band], IsAntialias = false };

            var rect = horizontal
                ? SKRect.Create(0, height / 3f * band, width, height / 3f)
                : SKRect.Create(width / 3f * band, 0, width / 3f, height);

            canvas.DrawRect(rect, paint);
        }

        canvas.Flush();

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    /// <summary>Reads back a stored rendition so its pixels can be asserted on.</summary>
    public static SKBitmap Decode(string path)
    {
        using var stream = File.OpenRead(path);
        return SKBitmap.Decode(stream) ?? throw new InvalidOperationException($"Could not decode {path}");
    }

    /// <summary>
    /// Which of the three bands a pixel came from. WebP is lossy, so the comparison is by
    /// dominant channel rather than exact value.
    /// </summary>
    public static string BandAt(SKBitmap bitmap, float xFraction, float yFraction)
    {
        var x = Math.Clamp((int)(bitmap.Width * xFraction), 0, bitmap.Width - 1);
        var y = Math.Clamp((int)(bitmap.Height * yFraction), 0, bitmap.Height - 1);
        var pixel = bitmap.GetPixel(x, y);

        if (pixel.Red > pixel.Green + 40 && pixel.Red > pixel.Blue + 40) return "red";
        if (pixel.Green > pixel.Red + 40 && pixel.Green > pixel.Blue + 40) return "green";
        if (pixel.Blue > pixel.Red + 40 && pixel.Blue > pixel.Green + 40) return "blue";
        return $"mixed(r{pixel.Red} g{pixel.Green} b{pixel.Blue})";
    }

    /// <summary>Every band present in the image, sampled on a grid.</summary>
    public static HashSet<string> BandsPresent(SKBitmap bitmap)
    {
        var found = new HashSet<string>();

        for (var i = 0; i <= 10; i++)
        {
            for (var j = 0; j <= 10; j++)
            {
                found.Add(BandAt(bitmap, i / 10f, j / 10f));
            }
        }

        return found;
    }
}
