using System.Text.Json;
using AuctionApp.Api.Domain;
using AuctionApp.Api.Domain.Entities;
using AuctionApp.Api.Services;
using Microsoft.EntityFrameworkCore;
using SkiaSharp;

namespace AuctionApp.Api.Data;

/// <summary>
/// Builds a throwaway badminton tournament for hands-on testing.
///
/// Every player gets a generated photo, and the shapes are mixed on purpose: portrait,
/// landscape and square, plus a couple of extreme aspect ratios. Real uploads will mostly be
/// portrait phone shots, but the layout has to survive someone dropping in a landscape team
/// photo — and the only way to see that is to have some.
///
/// Everything it creates belongs to one tournament, so deleting that tournament in the admin
/// console removes the lot.
/// </summary>
public static class MockDataSeeder
{
    public const string TournamentName = "Mock Badminton (test data)";

    private static readonly string[] Palette =
    [
        "#7c3aed", "#0ea5e9", "#059669", "#d97706", "#dc2626",
        "#0891b2", "#c026d3", "#4f46e5", "#65a30d", "#e11d48",
    ];

    /// <summary>
    /// Shapes each generated photo is cut to. Names are what the player is called, so the
    /// orientation under test is visible on screen without opening anything.
    /// </summary>
    private static readonly (string Label, int Width, int Height)[] Shapes =
    [
        ("portrait", 800, 1200),
        ("landscape", 1200, 800),
        ("square", 900, 900),
        ("tall", 600, 1600),
        ("wide", 1600, 600),
    ];

    public static async Task<int> SeedAsync(
        AuctionDbContext db,
        IPhotoService photos,
        ILogger logger,
        CancellationToken ct = default)
    {
        var existing = await db.Tournaments.FirstOrDefaultAsync(t => t.Name == TournamentName, ct);
        if (existing is not null)
        {
            logger.LogInformation("Mock data already present as tournament {Id}.", existing.Id);
            return existing.Id;
        }

        var sport = await db.Sports.FirstOrDefaultAsync(s => s.Name == "Badminton", ct)
            ?? throw new InvalidOperationException("Seed the sports before the mock data.");

        var tournament = new Tournament
        {
            Name = TournamentName,
            Description =
                "Generated for manual testing. Player photos cover portrait, landscape, square " +
                "and extreme aspect ratios. Delete this tournament to remove it all.",
            SportId = sport.Id,
            Type = TournamentType.Corporate,
            TeamPurse = 5_000_000m,
            MinSquadSize = 6,
            // No cap: office squads run to 15 or 20.
            MaxSquadSize = 0,
            MinWomenPerSquad = 2,
            MaxSpendPerPlayer = 2_000_000m,
            ReservePurseForUnfilledSlots = true,
        };

        db.Tournaments.Add(tournament);
        await db.SaveChangesAsync(ct);

        var teams = new[]
        {
            ("Platform Engineering", "PLT"),
            ("Data & Analytics", "DNA"),
            ("Client Services", "CLS"),
            ("Infrastructure", "INF"),
        };

        var created = new List<Team>();
        foreach (var (name, tag) in teams)
        {
            var team = new Team
            {
                TournamentId = tournament.Id,
                Name = name,
                ShortName = tag,
                Purse = tournament.TeamPurse,
                InitialPurse = tournament.TeamPurse,
            };

            db.Teams.Add(team);
            created.Add(team);
        }

        await db.SaveChangesAsync(ct);

        // Every team starts with an owner and two marquee players, none of whom are auctioned.
        var retainedNames = new[]
        {
            ("Owner", TeamRole.Owner),
            ("Marquee One", TeamRole.Marquee),
            ("Marquee Two", TeamRole.Marquee),
        };

        var shapeIndex = 0;
        foreach (var team in created)
        {
            foreach (var (suffix, role) in retainedNames)
            {
                var shape = Shapes[shapeIndex++ % Shapes.Length];
                var member = new Player
                {
                    Name = $"{team.ShortName} {suffix} ({shape.Label})",
                    TournamentId = tournament.Id,
                    Role = role == TeamRole.Owner ? "Team owner" : "Marquee player",
                    // Marquee signings are established players, and in these leagues that
                    // usually means men — which is exactly why the women rule still bites.
                    Gender = Gender.Male,
                    Country = "India",
                    BasePrice = 0m,
                    LotOrder = 0,
                    Status = PlayerStatus.Retained,
                    TeamRole = role,
                    SoldToTeamId = team.Id,
                    SkillsJson = JsonSerializer.Serialize(new Dictionary<string, string>
                    {
                        ["Grip"] = "Right-hand",
                        ["Discipline"] = role == TeamRole.Owner ? "Mixed doubles" : "Men's singles",
                    }),
                };

                member.PhotoKey = await StorePhotoAsync(photos, member.Name, shape, ct);
                db.Players.Add(member);
            }
        }

        // The auction pool: a deliberate mix of shapes and a real spread of genders.
        var pool = new (string Name, Gender Gender, decimal Base, string Discipline)[]
        {
            ("Priya Raghavan", Gender.Female, 900_000m, "Women's singles"),
            ("Aditya Menon", Gender.Male, 850_000m, "Men's singles"),
            ("Meera Krishnan", Gender.Female, 700_000m, "Women's doubles"),
            ("Rohit Bansal", Gender.Male, 650_000m, "Men's doubles"),
            ("Ananya Iyer", Gender.Female, 800_000m, "Mixed doubles"),
            ("Vikram Shetty", Gender.Male, 750_000m, "Men's doubles"),
            ("Fatima Sheikh", Gender.Female, 600_000m, "Women's doubles"),
            ("Karthik Nair", Gender.Male, 550_000m, "Mixed doubles"),
            ("Divya Prasad", Gender.Female, 500_000m, "Mixed doubles"),
            ("Sanjay Verma", Gender.Male, 450_000m, "Men's singles"),
            ("Lakshmi Rao", Gender.Female, 400_000m, "Women's doubles"),
            ("Imran Qureshi", Gender.Male, 350_000m, "Men's doubles"),
            ("Neha Kulkarni", Gender.Female, 320_000m, "Women's singles"),
            ("Arjun Pillai", Gender.Male, 300_000m, "Men's doubles"),
            ("Sneha Joshi", Gender.Female, 280_000m, "Mixed doubles"),
        };

        var lot = 1;
        foreach (var entry in pool)
        {
            var shape = Shapes[shapeIndex++ % Shapes.Length];

            var player = new Player
            {
                Name = $"{entry.Name} ({shape.Label})",
                TournamentId = tournament.Id,
                Role = entry.Discipline.Contains("singles") ? "Singles specialist" : "Doubles",
                Gender = entry.Gender,
                Country = "India",
                BasePrice = entry.Base,
                LotOrder = lot++,
                JerseyNumber = lot,
                Status = PlayerStatus.Available,
                SkillsJson = JsonSerializer.Serialize(new Dictionary<string, string>
                {
                    ["Grip"] = lot % 3 == 0 ? "Left-hand" : "Right-hand",
                    ["Discipline"] = entry.Discipline,
                    ["Ranking"] = $"Office #{lot}",
                }),
            };

            player.PhotoKey = await StorePhotoAsync(photos, player.Name, shape, ct);
            db.Players.Add(player);
        }

        db.Auctions.Add(new Auction
        {
            Name = "Mock badminton auction",
            TournamentId = tournament.Id,
            MinBidIncrement = 25_000m,
            Status = AuctionStatus.Draft,
            Visibility = AuctionVisibility.Public,
        });

        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "Seeded mock tournament {Id} with {Teams} teams, {Retained} retained members and {Pool} pool players.",
            tournament.Id,
            created.Count,
            created.Count * retainedNames.Length,
            pool.Length);

        return tournament.Id;
    }

    /// <summary>
    /// Paints a placeholder portrait at the requested shape and hands it to the real photo
    /// pipeline, so the stored renditions go through exactly the same cropping as an upload.
    /// </summary>
    private static async Task<string> StorePhotoAsync(
        IPhotoService photos,
        string name,
        (string Label, int Width, int Height) shape,
        CancellationToken ct)
    {
        var bytes = Paint(name, shape.Width, shape.Height);
        using var stream = new MemoryStream(bytes);
        return await photos.StoreAsync(stream, ct);
    }

    private static byte[] Paint(string name, int width, int height)
    {
        using var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);

        var seed = Math.Abs(name.GetHashCode());
        var background = SKColor.Parse(Palette[seed % Palette.Length]);

        // A vertical wash so the crop is visible: if the framing is wrong, the gradient will
        // be cut off somewhere obvious.
        using (var paint = new SKPaint())
        {
            paint.Shader = SKShader.CreateLinearGradient(
                new SKPoint(0, 0),
                new SKPoint(0, height),
                [background, background.WithAlpha(90)],
                null,
                SKShaderTileMode.Clamp);

            canvas.DrawRect(SKRect.Create(0, 0, width, height), paint);
        }

        // A head-and-shoulders silhouette in the upper third, where a real portrait puts a
        // face — this is what the top-biased crop is meant to keep.
        var unit = Math.Min(width, height) / 6f;
        using (var figure = new SKPaint { Color = SKColors.White.WithAlpha(215), IsAntialias = true })
        {
            canvas.DrawCircle(width / 2f, height * 0.28f, unit, figure);
            canvas.DrawRoundRect(
                SKRect.Create(width / 2f - unit * 1.4f, height * 0.28f + unit * 1.2f, unit * 2.8f, unit * 2.4f),
                unit * 0.6f,
                unit * 0.6f,
                figure);
        }

        // The shape stamped on the image, so a wrongly-cropped photo is obvious at a glance.
        using (var font = new SKFont(SKTypeface.Default, Math.Max(18f, unit * 0.42f)))
        using (var text = new SKPaint { Color = SKColors.White, IsAntialias = true })
        {
            canvas.DrawText($"{width}x{height}", width / 2f, height * 0.92f, SKTextAlign.Center, font, text);
        }

        canvas.Flush();

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 90);
        return data.ToArray();
    }
}
