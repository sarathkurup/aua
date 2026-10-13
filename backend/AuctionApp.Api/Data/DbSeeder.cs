using System.Text.Json;
using AuctionApp.Api.Domain;
using AuctionApp.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AuctionApp.Api.Data;

public class SeedOptions
{
    public const string SectionName = "Seed";

    /// <summary>Set false once the database has real accounts in it.</summary>
    public bool Enabled { get; set; } = true;

    public string AdminUsername { get; set; } = "admin";

    public string AdminPassword { get; set; } = "Admin@123";

    /// <summary>Shared password for the demo team-owner accounts.</summary>
    public string BidderPassword { get; set; } = "Team@123";

    /// <summary>Starting budget for each team.</summary>
    public decimal TeamPurse { get; set; } = 100_000_000m;
}

/// <summary>
/// Puts two runnable auctions in front of a fresh install: a professional cricket
/// tournament and an IT-company badminton tournament whose squad rules require women in
/// every team. Both can be started at the same time, which is the point — they exercise
/// the parallel-auction path out of the box.
/// </summary>
/// <remarks>
/// Runs at startup and is additive — it only inserts what is missing, so restarting the
/// app never overwrites live auction data. Password hashes are generated here rather
/// than through EF <c>HasData</c> because BCrypt output is non-deterministic and would
/// otherwise produce a new migration on every build.
/// </remarks>
public static class DbSeeder
{
    private const string Cricket = "Cricket";
    private const string Badminton = "Badminton";

    public static async Task SeedAsync(
        AuctionDbContext db,
        SeedOptions options,
        ILogger logger,
        CancellationToken ct = default)
    {
        if (!options.Enabled)
        {
            logger.LogInformation("Seeding disabled by configuration.");
            return;
        }

        var sports = await SeedSportsAsync(db, ct);
        var owners = await SeedUsersAsync(db, options, logger, ct);

        if (await db.Tournaments.AnyAsync(ct))
        {
            return;
        }

        // The seeded tournaments belong to the admin account, so the ownership rules have a
        // real owner to check against rather than a null that everyone can edit.
        var auctioneerId = await db.Users
            .Where(u => u.Username == options.AdminUsername)
            .Select(u => (int?)u.Id)
            .FirstOrDefaultAsync(ct);

        await SeedProfessionalCricketAsync(db, sports[Cricket], owners, auctioneerId, options, ct);
        await SeedCorporateBadmintonAsync(db, sports[Badminton], owners, auctioneerId, options, ct);

        logger.LogInformation("Seeded cricket and badminton tournaments.");
    }

    private static async Task<Dictionary<string, int>> SeedSportsAsync(AuctionDbContext db, CancellationToken ct)
    {
        var wanted = new[]
        {
            new Sport
            {
                Name = Cricket,
                Icon = "🏏",
                Description = "T20 franchise squad building.",
                SkillFieldsJson = Json(new[] { "Batting", "Bowling", "Fielding" }),
            },
            new Sport
            {
                Name = Badminton,
                Icon = "🏸",
                Description = "Singles, doubles and mixed-doubles squads.",
                SkillFieldsJson = Json(new[] { "Grip", "Discipline", "Ranking" }),
            },
        };

        var existing = await db.Sports.ToDictionaryAsync(s => s.Name, s => s, ct);

        foreach (var sport in wanted)
        {
            if (existing.TryGetValue(sport.Name, out var found))
            {
                // Skill fields are part of the app, not user data — keep them current.
                found.SkillFieldsJson ??= sport.SkillFieldsJson;
                found.Icon ??= sport.Icon;
            }
            else
            {
                db.Sports.Add(sport);
            }
        }

        await db.SaveChangesAsync(ct);
        return await db.Sports.ToDictionaryAsync(s => s.Name, s => s.Id, ct);
    }

    /// <summary>Returns the owner accounts keyed by username.</summary>
    private static async Task<Dictionary<string, int>> SeedUsersAsync(
        AuctionDbContext db,
        SeedOptions options,
        ILogger logger,
        CancellationToken ct)
    {
        if (!await db.Users.AnyAsync(ct))
        {
            db.Users.Add(new User
            {
                Username = options.AdminUsername,
                DisplayName = "Auction Control",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(options.AdminPassword),
                Role = UserRole.Admin,
            });

            foreach (var (username, displayName) in Owners)
            {
                db.Users.Add(new User
                {
                    Username = username,
                    DisplayName = displayName,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(options.BidderPassword),
                    Role = UserRole.TeamOwner,
                });
            }

            await db.SaveChangesAsync(ct);
            logger.LogWarning(
                "Seeded default accounts with well-known passwords. Change them before any real use.");
        }

        return await db.Users
            .Where(u => u.Role == UserRole.TeamOwner)
            .ToDictionaryAsync(u => u.Username, u => u.Id, ct);
    }

    private static readonly (string Username, string DisplayName)[] Owners =
    [
        ("coastal", "Nadia Alvarez"),
        ("metro", "Ibrahim Osei"),
        ("highland", "Sofia Lindqvist"),
        ("desert", "Rohan Mehta"),
    ];

    // ------------------------------------------------------------------ cricket

    private static async Task SeedProfessionalCricketAsync(
        AuctionDbContext db,
        int sportId,
        Dictionary<string, int> owners,
        int? auctioneerId,
        SeedOptions options,
        CancellationToken ct)
    {
        var tournament = new Tournament
        {
            OwnerUserId = auctioneerId,
            Name = "Premier T20 Season 1",
            Description = "Eight-team franchise league. Open competition, no composition rules.",
            SportId = sportId,
            Type = TournamentType.Professional,
            TeamPurse = options.TeamPurse,
            MinSquadSize = 3,
            MaxSquadSize = 6,
            MinWomenPerSquad = 0,
            MaxSpendPerPlayer = null,
            ReservePurseForUnfilledSlots = true,
        };

        db.Tournaments.Add(tournament);
        await db.SaveChangesAsync(ct);

        AddTeams(db, tournament, owners,
        [
            ("Coastal Titans", "CT", "coastal"),
            ("Metro Strikers", "MS", "metro"),
            ("Highland Rangers", "HR", "highland"),
            ("Desert Warriors", "DW", "desert"),
        ]);

        var players = new (string Name, string Role, Gender Gender, string Country, decimal Base, string Batting, string Bowling)[]
        {
            ("Arjun Deshmukh", "Batter", Gender.Male, "India", 20_000_000m, "Right-hand", "—"),
            ("Callum Reeves", "Fast bowler", Gender.Male, "England", 15_000_000m, "Left-hand", "Right-arm quick"),
            ("Sipho Ndlovu", "All-rounder", Gender.Male, "South Africa", 18_000_000m, "Right-hand", "Medium pace"),
            ("Tanvir Rahman", "Spinner", Gender.Male, "Bangladesh", 8_000_000m, "Left-hand", "Left-arm orthodox"),
            ("Wiremu Katene", "Wicketkeeper", Gender.Male, "New Zealand", 12_000_000m, "Right-hand", "—"),
            ("Diego Salcedo", "Batter", Gender.Male, "Argentina", 5_000_000m, "Left-hand", "—"),
            ("Hamza Iqbal", "Fast bowler", Gender.Male, "Pakistan", 14_000_000m, "Right-hand", "Right-arm quick"),
            ("Lachlan Ford", "All-rounder", Gender.Male, "Australia", 16_000_000m, "Right-hand", "Off-spin"),
            ("Ravi Kulkarni", "Spinner", Gender.Male, "India", 6_000_000m, "Right-hand", "Leg-spin"),
            ("Marcus Hollis", "Batter", Gender.Male, "West Indies", 10_000_000m, "Right-hand", "—"),
        };

        var lot = 1;
        foreach (var p in players)
        {
            db.Players.Add(new Player
            {
                Name = p.Name,
                TournamentId = tournament.Id,
                Role = p.Role,
                Gender = p.Gender,
                Country = p.Country,
                BasePrice = p.Base,
                LotOrder = lot++,
                SkillsJson = Json(new Dictionary<string, string>
                {
                    ["Batting"] = p.Batting,
                    ["Bowling"] = p.Bowling,
                }),
            });
        }

        db.Auctions.Add(new Auction
        {
            Name = "Premier T20 — main auction",
            TournamentId = tournament.Id,
            MinBidIncrement = 500_000m,
            Status = AuctionStatus.Draft,
            Visibility = AuctionVisibility.Public,
        });

        await db.SaveChangesAsync(ct);
    }

    // ------------------------------------------------------------------ badminton

    private static async Task SeedCorporateBadmintonAsync(
        AuctionDbContext db,
        int sportId,
        Dictionary<string, int> owners,
        int? auctioneerId,
        SeedOptions options,
        CancellationToken ct)
    {
        var tournament = new Tournament
        {
            OwnerUserId = auctioneerId,
            Name = "TechCorp Inter-Office Badminton",
            Description =
                "Workplace league. Every squad must field at least two women, and no squad may " +
                "exceed five players.",
            SportId = sportId,
            Type = TournamentType.Corporate,
            TeamPurse = 5_000_000m,
            MinSquadSize = 4,
            MaxSquadSize = 5,
            MinWomenPerSquad = 2,
            MaxSpendPerPlayer = 2_000_000m,
            ReservePurseForUnfilledSlots = true,
        };

        db.Tournaments.Add(tournament);
        await db.SaveChangesAsync(ct);

        AddTeams(db, tournament, owners,
        [
            ("Platform Engineering", "PLT", "coastal"),
            ("Data & Analytics", "DNA", "metro"),
            ("Client Services", "CLS", "highland"),
            ("Infrastructure", "INF", "desert"),
        ]);

        var players = new (string Name, string Role, Gender Gender, decimal Base, string Grip, string Discipline, string Ranking)[]
        {
            ("Priya Raghavan", "Singles specialist", Gender.Female, 900_000m, "Right-hand", "Women's singles", "Office #1"),
            ("Aditya Menon", "Singles specialist", Gender.Male, 850_000m, "Right-hand", "Men's singles", "Office #1"),
            ("Meera Krishnan", "Doubles", Gender.Female, 700_000m, "Left-hand", "Women's doubles", "Office #2"),
            ("Rohit Bansal", "Doubles", Gender.Male, 650_000m, "Right-hand", "Men's doubles", "Office #3"),
            ("Ananya Iyer", "All-court", Gender.Female, 800_000m, "Right-hand", "Mixed doubles", "Office #2"),
            ("Vikram Shetty", "Smash specialist", Gender.Male, 750_000m, "Right-hand", "Men's doubles", "Office #4"),
            ("Fatima Sheikh", "Defensive", Gender.Female, 600_000m, "Right-hand", "Women's doubles", "Office #5"),
            ("Karthik Nair", "All-court", Gender.Male, 550_000m, "Left-hand", "Mixed doubles", "Office #6"),
            ("Divya Prasad", "Net play", Gender.Female, 500_000m, "Right-hand", "Mixed doubles", "Office #4"),
            ("Sanjay Verma", "Singles specialist", Gender.Male, 450_000m, "Right-hand", "Men's singles", "Office #7"),
            ("Lakshmi Rao", "Doubles", Gender.Female, 400_000m, "Right-hand", "Women's doubles", "Office #6"),
            ("Imran Qureshi", "Smash specialist", Gender.Male, 350_000m, "Right-hand", "Men's doubles", "Office #8"),
        };

        var lot = 1;
        foreach (var p in players)
        {
            db.Players.Add(new Player
            {
                Name = p.Name,
                TournamentId = tournament.Id,
                Role = p.Role,
                Gender = p.Gender,
                Country = "India",
                BasePrice = p.Base,
                LotOrder = lot++,
                SkillsJson = Json(new Dictionary<string, string>
                {
                    ["Grip"] = p.Grip,
                    ["Discipline"] = p.Discipline,
                    ["Ranking"] = p.Ranking,
                }),
            });
        }

        // Private on purpose: a workplace draft is the case the unlisted mode exists for,
        // and seeding one means the share-link path is exercised from a fresh install.
        db.Auctions.Add(new Auction
        {
            Name = "TechCorp badminton draft",
            TournamentId = tournament.Id,
            MinBidIncrement = 25_000m,
            Status = AuctionStatus.Draft,
            Visibility = AuctionVisibility.Private,
        });

        await db.SaveChangesAsync(ct);
    }

    private static void AddTeams(
        AuctionDbContext db,
        Tournament tournament,
        Dictionary<string, int> owners,
        (string Name, string ShortName, string OwnerUsername)[] teams)
    {
        foreach (var (name, shortName, ownerUsername) in teams)
        {
            db.Teams.Add(new Team
            {
                TournamentId = tournament.Id,
                Name = name,
                ShortName = shortName,
                OwnerUserId = owners.TryGetValue(ownerUsername, out var id) ? id : null,
                Purse = tournament.TeamPurse,
                InitialPurse = tournament.TeamPurse,
            });
        }
    }

    private static string Json<T>(T value) => JsonSerializer.Serialize(value);
}
