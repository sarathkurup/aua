using AuctionApp.Api.Domain;
using AuctionApp.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AuctionApp.Api.Data.Repositories;

public class PlayerRepository : Repository<Player>, IPlayerRepository
{
    public PlayerRepository(AuctionDbContext db)
        : base(db)
    {
    }

    public async Task<Player?> GetWithDetailsAsync(int id, CancellationToken ct = default) =>
        await Set.Include(p => p.Tournament)
                .ThenInclude(t => t!.Sport)
            .Include(p => p.SoldToTeam)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<IReadOnlyList<Player>> ListForTournamentAsync(int tournamentId, CancellationToken ct = default) =>
        await Set.AsNoTracking()
            .Include(p => p.Tournament)
                .ThenInclude(t => t!.Sport)
            .Include(p => p.SoldToTeam)
            .Where(p => p.TournamentId == tournamentId)
            .OrderBy(p => p.LotOrder)
            .ThenBy(p => p.Name)
            .ToListAsync(ct);

    public async Task<decimal?> CheapestAvailableBaseAsync(
        int tournamentId,
        Gender? gender,
        CancellationToken ct = default)
    {
        // Available only: the lot on the block is the one being bid for, so it is not
        // part of what a team could fall back on afterwards.
        var query = Set.AsNoTracking()
            .Where(p => p.TournamentId == tournamentId && p.Status == PlayerStatus.Available);

        if (gender.HasValue)
        {
            query = query.Where(p => p.Gender == gender.Value);
        }

        // MinAsync would throw on an empty pool; the nullable projection returns null instead.
        return await query.Select(p => (decimal?)p.BasePrice).MinAsync(ct);
    }

    public async Task<IReadOnlyList<Player>> ListForAuctionAsync(int auctionId, CancellationToken ct = default) =>
        await Set.Where(p => p.AuctionId == auctionId).ToListAsync(ct);

    /// <remarks>
    /// Retained players are skipped. They were never in the pool — the owner and the marquee
    /// signings belong to their squad regardless of how many times the auction is re-run, and
    /// freeing them would put them up for sale.
    /// </remarks>
    public async Task<int> ResetPoolAsync(int tournamentId, CancellationToken ct = default) =>
        await Set.Where(p => p.TournamentId == tournamentId && p.Status != PlayerStatus.Retained)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(p => p.Status, PlayerStatus.Available)
                    .SetProperty(p => p.SoldPrice, (decimal?)null)
                    .SetProperty(p => p.SoldAt, (DateTime?)null)
                    .SetProperty(p => p.SoldToTeamId, (int?)null)
                    .SetProperty(p => p.AuctionId, (int?)null),
                ct);
}
