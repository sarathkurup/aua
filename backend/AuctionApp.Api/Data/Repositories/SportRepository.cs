using AuctionApp.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AuctionApp.Api.Data.Repositories;

public class SportRepository : Repository<Sport>, ISportRepository
{
    public SportRepository(AuctionDbContext db)
        : base(db)
    {
    }

    public async Task<IReadOnlyList<Sport>> ListWithTournamentCountsAsync(CancellationToken ct = default) =>
        await Set.AsNoTracking()
            .Include(s => s.Tournaments)
            .OrderBy(s => s.Name)
            .ToListAsync(ct);

    public async Task<bool> ExistsAsync(int id, CancellationToken ct = default) =>
        await Set.AnyAsync(s => s.Id == id, ct);
}
