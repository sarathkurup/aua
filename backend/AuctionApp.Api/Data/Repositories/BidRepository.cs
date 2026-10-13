using AuctionApp.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AuctionApp.Api.Data.Repositories;

public class BidRepository : Repository<Bid>, IBidRepository
{
    public BidRepository(AuctionDbContext db)
        : base(db)
    {
    }

    /// <summary>
    /// Bids that still count. A retracted bid keeps its row for the record but must not
    /// set the price, win a lot, or appear on the screen in the room — so every read path
    /// except the idempotency lookup starts here.
    /// </summary>
    private IQueryable<Bid> Standing => Set.Where(b => b.RetractedAt == null);

    public async Task<Bid?> GetTopBidAsync(int auctionId, int playerId, CancellationToken ct = default) =>
        await Standing.AsNoTracking()
            .Include(b => b.Team)
            .Include(b => b.Player)
            .Where(b => b.AuctionId == auctionId && b.PlayerId == playerId)
            .OrderByDescending(b => b.Amount)
            .ThenByDescending(b => b.Id)
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<Bid>> ListRecentAsync(int auctionId, int take, CancellationToken ct = default) =>
        await Standing.AsNoTracking()
            .Include(b => b.Team)
            .Include(b => b.Player)
            .Where(b => b.AuctionId == auctionId)
            .OrderByDescending(b => b.Id)
            .Take(take)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Bid>> ListForPlayerAsync(int auctionId, int playerId, CancellationToken ct = default) =>
        await Standing.AsNoTracking()
            .Include(b => b.Team)
            .Include(b => b.Player)
            .Where(b => b.AuctionId == auctionId && b.PlayerId == playerId)
            .OrderByDescending(b => b.Amount)
            .ToListAsync(ct);

    /// <summary>
    /// The standing bid on a player, tracked so it can be retracted.
    /// </summary>
    public async Task<Bid?> GetTopBidForUpdateAsync(int auctionId, int playerId, CancellationToken ct = default) =>
        await Standing
            .Include(b => b.Team)
            .Include(b => b.Player)
            .Where(b => b.AuctionId == auctionId && b.PlayerId == playerId)
            .OrderByDescending(b => b.Amount)
            .ThenByDescending(b => b.Id)
            .FirstOrDefaultAsync(ct);

    /// <remarks>
    /// Deliberately unfiltered. The unique index on (AuctionId, ClientRequestId) covers
    /// retracted rows too, so a retry carrying a retracted key has to resolve to that row
    /// rather than attempt an insert the database will reject.
    /// </remarks>
    public async Task<Bid?> FindByClientRequestIdAsync(int auctionId, string clientRequestId, CancellationToken ct = default) =>
        await Set.AsNoTracking()
            .Include(b => b.Team)
            .Include(b => b.Player)
            .FirstOrDefaultAsync(b => b.AuctionId == auctionId && b.ClientRequestId == clientRequestId, ct);

    public async Task<int> DeleteForPlayerAsync(int playerId, CancellationToken ct = default) =>
        await Set.Where(b => b.PlayerId == playerId).ExecuteDeleteAsync(ct);
}
