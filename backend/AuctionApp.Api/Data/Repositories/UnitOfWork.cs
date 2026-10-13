using Microsoft.EntityFrameworkCore;

namespace AuctionApp.Api.Data.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly AuctionDbContext _db;

    public UnitOfWork(
        AuctionDbContext db,
        ISportRepository sports,
        ITournamentRepository tournaments,
        ITeamRepository teams,
        IPlayerRepository players,
        IAuctionRepository auctions,
        IBidRepository bids,
        IUserRepository users)
    {
        _db = db;
        Sports = sports;
        Tournaments = tournaments;
        Teams = teams;
        Players = players;
        Auctions = auctions;
        Bids = bids;
        Users = users;
    }

    public ISportRepository Sports { get; }

    public ITournamentRepository Tournaments { get; }

    public ITeamRepository Teams { get; }

    public IPlayerRepository Players { get; }

    public IAuctionRepository Auctions { get; }

    public IBidRepository Bids { get; }

    public IUserRepository Users { get; }

    public Task<int> SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);

    public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, CancellationToken ct = default)
    {
        var strategy = _db.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(
            async (CancellationToken token) =>
            {
                await using var tx = await _db.Database.BeginTransactionAsync(token);
                await operation(token);
                await tx.CommitAsync(token);
            },
            ct);
    }
}
