using System.Security.Cryptography;
using AuctionApp.Api.Domain;
using SkiaSharp;

namespace AuctionApp.Api.Services;

public interface IPhotoService
{
    /// <summary>
    /// Writes two square WebP renditions of the image and returns the content-derived key
    /// they are named after. Re-uploading the same picture yields the same key.
    /// </summary>
    Task<string> StoreAsync(Stream image, CancellationToken ct = default);

    void Delete(string key);
}

/// <summary>
/// Player photos, resized once at upload so no screen ever downloads a phone-camera JPEG.
///
/// Two square renditions are produced: a 96px thumbnail for tables and the bid feed, and a
/// 480px crop for the on-block card. The filename is a hash of the source bytes, which makes
/// every URL immutable — so they are served with a one-year cache and a client that has seen
/// a player once never fetches them again.
/// </summary>
public class PhotoService : IPhotoService
{
    /// <summary>
    /// Where the two renditions are written, relative to the content root.
    ///
    /// Deliberately outside wwwroot: the default static-file provider would otherwise serve
    /// these first and they would lose the immutable cache header the explicit /media
    /// mapping in Program.cs applies. That header is the whole point of hashing the names.
    /// </summary>
    public const string RelativeDirectory = "media/players";

    private const int ThumbSize = 96;
    private const int CardSize = 480;

    /// <summary>
    /// Portraits put the face near the top, so a square crop taken from the middle tends to
    /// cut foreheads. Bias the window upwards instead.
    /// </summary>
    private const float PortraitCropBias = 0.15f;

    /// <summary>Refuse anything larger than a generous phone photo before decoding it.</summary>
    public const long MaxUploadBytes = 8 * 1024 * 1024;

    private static readonly SKSamplingOptions Sampling = new(SKCubicResampler.Mitchell);

    private readonly string _root;
    private readonly ILogger<PhotoService> _logger;

    public PhotoService(IWebHostEnvironment environment, ILogger<PhotoService> logger)
    {
        // Derived from the content root rather than WebRootPath: on a first run wwwroot may
        // not exist yet, and WebRootPath is null in that case.
        _root = Path.Combine(environment.ContentRootPath, RelativeDirectory);
        _logger = logger;
        Directory.CreateDirectory(_root);
    }

    public async Task<string> StoreAsync(Stream image, CancellationToken ct = default)
    {
        // Buffer once: the bytes are needed twice, for the hash and for the decode, and the
        // upload stream is forward-only.
        using var buffer = new MemoryStream();
        await image.CopyToAsync(buffer, ct);

        var bytes = buffer.ToArray();
        if (bytes.Length == 0)
        {
            throw new AuctionRuleException("The uploaded file is empty.");
        }

        var key = Convert.ToHexString(SHA256.HashData(bytes))[..16].ToLowerInvariant();

        var thumbPath = PathFor(key, "sm");
        var cardPath = PathFor(key, "lg");

        if (File.Exists(thumbPath) && File.Exists(cardPath))
        {
            _logger.LogInformation("Photo {Key} already stored; reusing it", key);
            return key;
        }

        // Create the codec first and decode through it. SKBitmap.Decode(byte[]) throws
        // from inside Skia when it cannot identify the format, which is not something to
        // hand back to the user as a 500 — a null codec is the clean "not an image" signal.
        using var stream = new MemoryStream(bytes);
        using var codec = SKCodec.Create(stream)
            ?? throw new AuctionRuleException("That file is not an image the server can read.");

        using var decoded = SKBitmap.Decode(codec)
            ?? throw new AuctionRuleException("That image could not be decoded.");

        // Phone cameras record rotation in EXIF rather than in the pixels.
        using var upright = ApplyOrientation(decoded, codec.EncodedOrigin);

        WriteSquare(upright, CardSize, cardPath, quality: 82);
        WriteSquare(upright, ThumbSize, thumbPath, quality: 78);

        _logger.LogInformation("Stored photo {Key}", key);
        return key;
    }

    public void Delete(string key)
    {
        foreach (var size in new[] { "sm", "lg" })
        {
            var path = PathFor(key, size);
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch (IOException ex)
            {
                // A stale file costs disk, not correctness — never fail the request over it.
                _logger.LogWarning(ex, "Could not delete photo {Path}", path);
            }
        }
    }

    /// <summary>Square-crops and scales in a single draw, then encodes to WebP.</summary>
    private static void WriteSquare(SKBitmap source, int size, string path, int quality)
    {
        var side = Math.Min(source.Width, source.Height);
        var left = (source.Width - side) / 2f;
        var top = source.Height > source.Width
            ? (source.Height - side) * PortraitCropBias
            : (source.Height - side) / 2f;

        var sourceRect = SKRect.Create(left, top, side, side);
        var targetRect = SKRect.Create(0, 0, size, size);

        var info = new SKImageInfo(size, size, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info);
        surface.Canvas.Clear(SKColors.Transparent);

        using (var image = SKImage.FromBitmap(source))
        {
            surface.Canvas.DrawImage(image, sourceRect, targetRect, Sampling);
        }

        using var snapshot = surface.Snapshot();
        using var data = snapshot.Encode(SKEncodedImageFormat.Webp, quality);
        using var file = File.Create(path);
        data.SaveTo(file);
    }

    /// <summary>Rotates and flips the decoded pixels so the stored image is the right way up.</summary>
    private static SKBitmap ApplyOrientation(SKBitmap source, SKEncodedOrigin origin)
    {
        if (origin is SKEncodedOrigin.TopLeft)
        {
            return source.Copy();
        }

        var swapsAxes = origin is SKEncodedOrigin.LeftTop
            or SKEncodedOrigin.RightTop
            or SKEncodedOrigin.RightBottom
            or SKEncodedOrigin.LeftBottom;

        var width = swapsAxes ? source.Height : source.Width;
        var height = swapsAxes ? source.Width : source.Height;

        var rotated = new SKBitmap(width, height, source.ColorType, source.AlphaType);
        using var canvas = new SKCanvas(rotated);

        // Each case maps the source corner named by the origin onto the top-left.
        var matrix = origin switch
        {
            SKEncodedOrigin.TopRight => SKMatrix.CreateScale(-1, 1).PostConcat(SKMatrix.CreateTranslation(width, 0)),
            SKEncodedOrigin.BottomRight => SKMatrix.CreateRotationDegrees(180).PostConcat(SKMatrix.CreateTranslation(width, height)),
            SKEncodedOrigin.BottomLeft => SKMatrix.CreateScale(1, -1).PostConcat(SKMatrix.CreateTranslation(0, height)),
            SKEncodedOrigin.LeftTop => SKMatrix.CreateRotationDegrees(90).PostConcat(SKMatrix.CreateScale(-1, 1)).PostConcat(SKMatrix.CreateTranslation(width, 0)),
            SKEncodedOrigin.RightTop => SKMatrix.CreateRotationDegrees(90).PostConcat(SKMatrix.CreateTranslation(width, 0)),
            SKEncodedOrigin.RightBottom => SKMatrix.CreateRotationDegrees(-90).PostConcat(SKMatrix.CreateScale(-1, 1)).PostConcat(SKMatrix.CreateTranslation(0, height)),
            SKEncodedOrigin.LeftBottom => SKMatrix.CreateRotationDegrees(-90).PostConcat(SKMatrix.CreateTranslation(0, height)),
            _ => SKMatrix.Identity,
        };

        canvas.SetMatrix(matrix);
        canvas.DrawBitmap(source, new SKPoint(0, 0), Sampling);
        canvas.Flush();

        return rotated;
    }

    private string PathFor(string key, string size) => Path.Combine(_root, $"{key}-{size}.webp");
}
