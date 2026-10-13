using AuctionApp.Api.Domain;
using AuctionApp.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AuctionApp.Api.Data.Repositories;

public class TournamentRepository : Repository<Tournament>, ITournamentRepository
{
    public TournamentRepository(AuctionDbContext db)
        : base(db)
    {
    }

    public async Task<Tournament?> GetWithDetailsAsync(int id, CancellationToken ct = default) =>
        await Set.Include(t => t.Sport)
            .Include(t => t.Owner)
            .FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task<IReadOnlyList<Tournament>> ListWithSportAsync(CancellationToken ct = default) =>
        await Set.AsNoTracking()
            .Include(t => t.Sport)
            .Include(t => t.Owner)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(ct);
}

public class TeamRepository : Repository<Team>, ITeamRepository
{
    public TeamRepository(AuctionDbContext db)
        : base(db)
    {
    }

    public async Task<IReadOnlyList<Team>> ListForTournamentAsync(int tournamentId, CancellationToken ct = default) =>
        await Set.AsNoTracking()
            .Include(t => t.Players)
            .Include(t => t.Owner)
            .Where(t => t.TournamentId == tournamentId && t.IsActive)
            .OrderBy(t => t.Name)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Team>> ListForOwnerAsync(int ownerUserId, CancellationToken ct = default) =>
        await Set.AsNoTracking()
            .Include(t => t.Tournament)
                .ThenInclude(t => t!.Sport)
            .Where(t => t.OwnerUserId == ownerUserId && t.IsActive)
            .OrderByDescending(t => t.TournamentId)
            .ToListAsync(ct);

    public async Task<Team?> GetWithSquadAsync(int teamId, CancellationToken ct = default) =>
        await Set.Include(t => t.Players)
            .Include(t => t.Tournament)
            .FirstOrDefaultAsync(t => t.Id == teamId, ct);

    public async Task<int> ResetPursesAsync(int tournamentId, CancellationToken ct = default) =>
        await Set.Where(t => t.TournamentId == tournamentId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(t => t.Purse, t => t.InitialPurse), ct);
}
