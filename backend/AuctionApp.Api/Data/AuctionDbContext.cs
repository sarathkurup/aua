using AuctionApp.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace AuctionApp.Api.Data;

public class AuctionDbContext : DbContext
{
    public AuctionDbContext(DbContextOptions<AuctionDbContext> options)
        : base(options)
    {
    }

    public DbSet<Sport> Sports => Set<Sport>();

    public DbSet<Tournament> Tournaments => Set<Tournament>();

    public DbSet<Team> Teams => Set<Team>();

    public DbSet<Player> Players => Set<Player>();

    public DbSet<Auction> Auctions => Set<Auction>();

    public DbSet<Bid> Bids => Set<Bid>();

    public DbSet<User> Users => Set<User>();

    public DbSet<AccountRequest> AccountRequests => Set<AccountRequest>();

    /// <summary>
    /// Every instant in this app is UTC, but SQL Server's <c>datetime2</c> carries no zone,
    /// so EF hands values back as <see cref="DateTimeKind.Unspecified"/>. System.Text.Json
    /// then writes them without a trailing "Z", and a browser reads that as *local* time —
    /// showing an 18:00 UTC auction as 18:00 wherever the viewer happens to be. Stamping the
    /// kind on the way out of the database makes the API emit real UTC instants.
    /// </summary>
    private static readonly ValueConverter<DateTime, DateTime> UtcConverter =
        new(value => value, value => DateTime.SpecifyKind(value, DateTimeKind.Utc));

    private static readonly ValueConverter<DateTime?, DateTime?> NullableUtcConverter =
        new(
            value => value,
            value => value.HasValue ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc) : value);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(entity =>
        {
            entity.Property(u => u.Username).IsRequired().HasMaxLength(64);
            entity.Property(u => u.DisplayName).IsRequired().HasMaxLength(128);
            entity.Property(u => u.PasswordHash).IsRequired().HasMaxLength(256);
            entity.Property(u => u.Email).HasMaxLength(256);
            entity.Property(u => u.PhoneNumber).HasMaxLength(32);
            entity.Property(u => u.AvatarUrl).HasMaxLength(512);
            entity.HasIndex(u => u.Username).IsUnique();
            entity.HasIndex(u => u.PublicId).IsUnique();
        });

        modelBuilder.Entity<Sport>(entity =>
        {
            entity.Property(s => s.Name).IsRequired().HasMaxLength(64);
            entity.Property(s => s.Description).HasMaxLength(512);
            entity.Property(s => s.Icon).HasMaxLength(16);
            entity.Property(s => s.SkillFieldsJson).HasMaxLength(512);
            entity.HasIndex(s => s.Name).IsUnique();
        });

        modelBuilder.Entity<Tournament>(entity =>
        {
            entity.Property(t => t.Name).IsRequired().HasMaxLength(128);
            entity.Property(t => t.Description).HasMaxLength(512);
            entity.Property(t => t.TeamPurse).HasPrecision(18, 2);
            entity.Property(t => t.MaxSpendPerPlayer).HasPrecision(18, 2);

            entity.HasOne(t => t.Sport)
                .WithMany(s => s.Tournaments)
                .HasForeignKey(t => t.SportId)
                .OnDelete(DeleteBehavior.Restrict);

            // Deleting an account must not take its tournaments — and everything hanging
            // off them — with it. The tournament is left ownerless for an admin to reassign.
            entity.HasOne(t => t.Owner)
                .WithMany(u => u.TournamentsOwned)
                .HasForeignKey(t => t.OwnerUserId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(t => t.OwnerUserId);
        });

        modelBuilder.Entity<Team>(entity =>
        {
            entity.Property(t => t.Name).IsRequired().HasMaxLength(128);
            entity.Property(t => t.ShortName).HasMaxLength(8);
            entity.Property(t => t.Purse).HasPrecision(18, 2);
            entity.Property(t => t.InitialPurse).HasPrecision(18, 2);

            entity.HasOne(t => t.Tournament)
                .WithMany(t => t.Teams)
                .HasForeignKey(t => t.TournamentId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(t => t.Owner)
                .WithMany(u => u.Teams)
                .HasForeignKey(t => t.OwnerUserId)
                .OnDelete(DeleteBehavior.SetNull);

            // One team name per tournament, and one team per owner per tournament.
            entity.HasIndex(t => new { t.TournamentId, t.Name }).IsUnique();
            entity.HasIndex(t => new { t.TournamentId, t.OwnerUserId });
        });

        modelBuilder.Entity<Player>(entity =>
        {
            entity.Property(p => p.Name).IsRequired().HasMaxLength(128);
            entity.Property(p => p.Role).HasMaxLength(64);
            entity.Property(p => p.Country).HasMaxLength(64);
            entity.Property(p => p.SkillsJson).HasMaxLength(1024);
            entity.Property(p => p.PhotoKey).HasMaxLength(64);
            entity.Property(p => p.ImageUrl).HasMaxLength(512);
            entity.Property(p => p.BasePrice).HasPrecision(18, 2);
            entity.Property(p => p.SoldPrice).HasPrecision(18, 2);

            // NoAction, not Cascade: SQL Server would otherwise see two delete paths from
            // Tournament to Players — one direct, one through Auctions. TournamentService
            // clears the pool explicitly instead.
            entity.HasOne(p => p.Tournament)
                .WithMany(t => t.Players)
                .HasForeignKey(p => p.TournamentId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(p => p.SoldToTeam)
                .WithMany(t => t.Players)
                .HasForeignKey(p => p.SoldToTeamId)
                .OnDelete(DeleteBehavior.NoAction);

            // A player is stamped with the auction that resolved them. Clearing the
            // auction must not delete the player, so the FK nulls out instead.
            entity.HasOne(p => p.Auction)
                .WithMany(a => a.Players)
                .HasForeignKey(p => p.AuctionId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(p => new { p.TournamentId, p.Status });
            entity.HasIndex(p => new { p.TournamentId, p.LotOrder });
        });

        modelBuilder.Entity<Auction>(entity =>
        {
            entity.Property(a => a.Name).IsRequired().HasMaxLength(128);
            entity.Property(a => a.MinBidIncrement).HasPrecision(18, 2);
            entity.Property(a => a.MinimumBid).HasPrecision(18, 2);
            entity.Property(a => a.IncrementRulesJson).HasMaxLength(2048);

            entity.HasOne(a => a.Tournament)
                .WithMany(t => t.Auctions)
                .HasForeignKey(a => a.TournamentId)
                .OnDelete(DeleteBehavior.Cascade);

            // Second relationship to Player, distinct from Auction.Players. NoAction
            // keeps SQL Server from seeing a multiple-cascade-path cycle.
            entity.HasOne(a => a.CurrentPlayer)
                .WithMany()
                .HasForeignKey(a => a.CurrentPlayerId)
                .OnDelete(DeleteBehavior.NoAction);

            // Serves the lobby query: every public auction currently live or paused.
            entity.HasIndex(a => new { a.Status, a.Visibility });

            // Every spectator request arrives keyed on this, so it has to be indexed.
            // Unique because it is the room's address.
            entity.HasIndex(a => a.PublicId).IsUnique();
        });

        modelBuilder.Entity<AccountRequest>(entity =>
        {
            entity.Property(r => r.Username).IsRequired().HasMaxLength(64);
            entity.Property(r => r.DisplayName).IsRequired().HasMaxLength(128);
            entity.Property(r => r.AuctionName).HasMaxLength(128);
            entity.Property(r => r.Contact).HasMaxLength(256);
            entity.Property(r => r.DecisionNote).HasMaxLength(512);

            entity.HasOne(r => r.DecidedByUser)
                .WithMany()
                .HasForeignKey(r => r.DecidedByUserId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(r => r.CreatedUser)
                .WithMany()
                .HasForeignKey(r => r.CreatedUserId)
                .OnDelete(DeleteBehavior.NoAction);

            // Serves the console's default view: what still needs a decision.
            entity.HasIndex(r => r.Status);

            // One live request per username. Declined ones are left alone so a name can be
            // asked for again after a refusal.
            entity.HasIndex(r => r.Username)
                .IsUnique()
                .HasFilter("[Status] = 0");
        });

        modelBuilder.Entity<Bid>(entity =>
        {
            entity.Property(b => b.Amount).HasPrecision(18, 2);

            entity.HasOne(b => b.Auction)
                .WithMany(a => a.Bids)
                .HasForeignKey(b => b.AuctionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(b => b.Player)
                .WithMany(p => p.Bids)
                .HasForeignKey(b => b.PlayerId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(b => b.Team)
                .WithMany(t => t.Bids)
                .HasForeignKey(b => b.TeamId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(b => b.PlacedByUser)
                .WithMany()
                .HasForeignKey(b => b.PlacedByUserId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(b => b.RetractedByUser)
                .WithMany()
                .HasForeignKey(b => b.RetractedByUserId)
                .OnDelete(DeleteBehavior.NoAction);

            // Computed from RetractedAt, never stored.
            entity.Ignore(b => b.IsRetracted);

            // Serves the hot query: highest standing bid for the player on the block.
            // Retracted rows are filtered out of it, so they cost nothing to skip.
            entity.HasIndex(b => new { b.AuctionId, b.PlayerId, b.Amount });

            entity.Property(b => b.ClientRequestId).HasMaxLength(64);

            // Filtered unique index: enforces retry idempotency without forcing every
            // bid to carry a key.
            entity.HasIndex(b => new { b.AuctionId, b.ClientRequestId })
                .IsUnique()
                .HasFilter("[ClientRequestId] IS NOT NULL");
        });

        // Applied last so it covers every DateTime the configurations above declared.
        // Converts on read only, so no column type changes and no migration.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTime))
                {
                    property.SetValueConverter(UtcConverter);
                }
                else if (property.ClrType == typeof(DateTime?))
                {
                    property.SetValueConverter(NullableUtcConverter);
                }
            }
        }
    }
}
