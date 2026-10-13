using AuctionApp.Api.Domain;
using AuctionApp.Api.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using SkiaSharp;

namespace AuctionApp.Tests;

/// <summary>
/// Uploads are whatever a phone or a laptop happened to produce. These pin down what the
/// server does with each shape, because the UI relies on every rendition being square.
/// </summary>
public class PhotoServiceTests : IDisposable
{
    private readonly string _root;
    private readonly PhotoService _photos;

    public PhotoServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "auction-photo-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        _photos = new PhotoService(new StubEnvironment(_root), NullLogger<PhotoService>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        GC.SuppressFinalize(this);
    }

    private string PathFor(string key, string size) =>
        Path.Combine(_root, PhotoService.RelativeDirectory, $"{key}-{size}.webp");

    private async Task<string> StoreAsync(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        return await _photos.StoreAsync(stream);
    }

    [Theory]
    [InlineData("portrait")]
    [InlineData("landscape")]
    [InlineData("square")]
    public async Task Stores_both_renditions_as_squares_whatever_the_source_shape(string shape)
    {
        var bytes = shape switch
        {
            "portrait" => TestImages.Portrait(),
            "landscape" => TestImages.Landscape(),
            _ => TestImages.Square(),
        };

        var key = await StoreAsync(bytes);

        using var thumb = TestImages.Decode(PathFor(key, "sm"));
        using var card = TestImages.Decode(PathFor(key, "lg"));

        Assert.Equal(96, thumb.Width);
        Assert.Equal(96, thumb.Height);
        Assert.Equal(480, card.Width);
        Assert.Equal(480, card.Height);
    }

    /// <summary>
    /// A portrait is cropped towards the top, because that is where faces sit. The bottom
    /// band is the proof: a centred crop of a 400x800 image would reach into it, a top-biased
    /// one never does.
    /// </summary>
    [Fact]
    public async Task Portrait_is_cropped_towards_the_top_and_never_reaches_the_bottom()
    {
        var key = await StoreAsync(TestImages.Portrait(400, 800));

        using var card = TestImages.Decode(PathFor(key, "lg"));
        var bands = TestImages.BandsPresent(card);

        Assert.Contains("red", bands);
        Assert.DoesNotContain("blue", bands);
        Assert.Equal("red", TestImages.BandAt(card, 0.5f, 0.05f));
    }

    /// <summary>A landscape is cropped through the horizontal centre, so the middle survives.</summary>
    [Fact]
    public async Task Landscape_is_cropped_through_the_middle()
    {
        var key = await StoreAsync(TestImages.Landscape(900, 300));

        using var card = TestImages.Decode(PathFor(key, "lg"));

        Assert.Equal("green", TestImages.BandAt(card, 0.5f, 0.5f));

        // A 300-tall square out of a 900-wide source spans x = 300..600, which is exactly
        // the middle band — the outer bands must not appear at all.
        var bands = TestImages.BandsPresent(card);
        Assert.DoesNotContain("red", bands);
        Assert.DoesNotContain("blue", bands);
    }

    [Fact]
    public async Task The_same_picture_twice_yields_the_same_key()
    {
        var bytes = TestImages.Portrait();

        var first = await StoreAsync(bytes);
        var second = await StoreAsync(bytes);

        Assert.Equal(first, second);
    }

    [Fact]
    public async Task Different_pictures_yield_different_keys()
    {
        var portrait = await StoreAsync(TestImages.Portrait());
        var landscape = await StoreAsync(TestImages.Landscape());

        Assert.NotEqual(portrait, landscape);
    }

    [Fact]
    public async Task A_file_that_is_not_an_image_is_refused_with_a_readable_message()
    {
        var bytes = "this is a spreadsheet, not a photo"u8.ToArray();

        var error = await Assert.ThrowsAsync<AuctionRuleException>(() => StoreAsync(bytes));

        Assert.Contains("not an image", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task An_empty_upload_is_refused()
    {
        await Assert.ThrowsAsync<AuctionRuleException>(() => StoreAsync([]));
    }

    [Fact]
    public async Task Deleting_removes_both_renditions()
    {
        var key = await StoreAsync(TestImages.Portrait());
        Assert.True(File.Exists(PathFor(key, "sm")));

        _photos.Delete(key);

        Assert.False(File.Exists(PathFor(key, "sm")));
        Assert.False(File.Exists(PathFor(key, "lg")));
    }

    /// <summary>A huge upload is still reduced to the two fixed sizes, so screens stay fast.</summary>
    [Fact]
    public async Task A_large_upload_is_reduced_to_the_fixed_sizes()
    {
        var key = await StoreAsync(TestImages.Portrait(2400, 3600));

        var thumbBytes = new FileInfo(PathFor(key, "sm")).Length;
        var cardBytes = new FileInfo(PathFor(key, "lg")).Length;

        using var thumb = TestImages.Decode(PathFor(key, "sm"));
        Assert.Equal(96, thumb.Width);

        // Flat colour compresses hard; the point is that neither is anywhere near the source.
        Assert.True(thumbBytes < 20_000, $"thumbnail was {thumbBytes} bytes");
        Assert.True(cardBytes < 200_000, $"card was {cardBytes} bytes");
    }

    /// <summary>Minimal host environment: PhotoService only ever reads the content root.</summary>
    private sealed class StubEnvironment : IWebHostEnvironment
    {
        public StubEnvironment(string root)
        {
            ContentRootPath = root;
            WebRootPath = Path.Combine(root, "wwwroot");
            ContentRootFileProvider = new NullFileProvider();
            WebRootFileProvider = new NullFileProvider();
        }

        public string ApplicationName { get; set; } = "AuctionApp.Tests";

        public IFileProvider ContentRootFileProvider { get; set; }

        public string ContentRootPath { get; set; }

        public string EnvironmentName { get; set; } = "Test";

        public IFileProvider WebRootFileProvider { get; set; }

        public string WebRootPath { get; set; }
    }
}
