using AuctionApp.Api.Domain;
using AuctionApp.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AuctionApp.Api.Data.Repositories;

public class AuctionRepository : Repository<Auction>, IAuctionRepository
{
    public AuctionRepository(AuctionDbContext db)
        : base(db)
    {
    }

    public async Task<Auction?> GetWithDetailsAsync(int id, CancellationToken ct = default) =>
        await Set.Include(a => a.Tournament)
                .ThenInclude(t => t!.Sport)
            .Include(a => a.CurrentPlayer)
                .ThenInclude(p => p!.SoldToTeam)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task<IReadOnlyList<Auction>> ListWithTournamentAsync(CancellationToken ct = default) =>
        await Set.AsNoTracking()
            .Include(a => a.Tournament)
                .ThenInclude(t => t!.Sport)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(ct);

    /// <summary>
    /// Feeds the public lobby, so private rooms are excluded here rather than filtered by
    /// the caller — this is the single place the "unlisted" promise is kept.
    /// </summary>
    public async Task<IReadOnlyList<Auction>> ListRunningAsync(CancellationToken ct = default) =>
        await Set.AsNoTracking()
            .Include(a => a.Tournament)
                .ThenInclude(t => t!.Sport)
            .Include(a => a.CurrentPlayer)
            .Where(a => (a.Status == AuctionStatus.Live || a.Status == AuctionStatus.Paused) &&
                        a.Visibility == AuctionVisibility.Public)
            .OrderByDescending(a => a.StartedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Auction>> ListUpcomingAsync(CancellationToken ct = default) =>
        await Set.AsNoTracking()
            .Include(a => a.Tournament)
                .ThenInclude(t => t!.Sport)
            .Where(a => a.Status == AuctionStatus.Draft && a.ScheduledStartAt != null)
            .OrderBy(a => a.ScheduledStartAt)
            .ToListAsync(ct);

    /// <summary>
    /// Feeds the retention sweep. Tracked, because the rows are about to be deleted.
    /// </summary>
    public async Task<IReadOnlyList<Auction>> ListExpiredAsync(DateTime endedBefore, CancellationToken ct = default) =>
        await Set.Where(a => a.Status == AuctionStatus.Completed &&
                             a.EndedAt != null &&
                             a.EndedAt < endedBefore)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<int>> ListTimedOutLotsAsync(DateTime asOf, CancellationToken ct = default) =>
        await Set.AsNoTracking()
            .Where(a => a.Status == AuctionStatus.Live &&
                        a.CurrentPlayerId != null &&
                        a.LotDeadlineAt != null &&
                        a.LotDeadlineAt <= asOf)
            .Select(a => a.Id)
            .ToListAsync(ct);

    public async Task<Auction?> GetByPublicIdAsync(Guid publicId, CancellationToken ct = default) =>
        await Set.Include(a => a.Tournament)
                .ThenInclude(t => t!.Sport)
            .Include(a => a.CurrentPlayer)
                .ThenInclude(p => p!.SoldToTeam)
            .FirstOrDefaultAsync(a => a.PublicId == publicId, ct);

    /// <summary>
    /// Auctions run in parallel across tournaments, but two at once inside the same
    /// tournament would let both spend the same purses, so that stays blocked.
    /// </summary>
    public async Task<Auction?> GetRunningForTournamentAsync(int tournamentId, CancellationToken ct = default) =>
        await Set.Include(a => a.Tournament)
            .Where(a => a.TournamentId == tournamentId &&
                        (a.Status == AuctionStatus.Live || a.Status == AuctionStatus.Paused))
            .OrderByDescending(a => a.StartedAt)
            .FirstOrDefaultAsync(ct);
}
