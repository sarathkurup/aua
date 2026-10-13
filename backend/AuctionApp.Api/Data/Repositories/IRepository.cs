using AuctionApp.Api.Domain;
using AuctionApp.Api.Domain.Entities;

namespace AuctionApp.Api.Data.Repositories;

/// <summary>Shared read/write operations over an aggregate root.</summary>
public interface IRepository<T>
    where T : class
{
    Task<T?> GetByIdAsync(int id, CancellationToken ct = default);

    Task<IReadOnlyList<T>> ListAllAsync(CancellationToken ct = default);

    Task AddAsync(T entity, CancellationToken ct = default);

    void Update(T entity);

    void Remove(T entity);
}

public interface ISportRepository : IRepository<Sport>
{
    Task<IReadOnlyList<Sport>> ListWithTournamentCountsAsync(CancellationToken ct = default);

    Task<bool> ExistsAsync(int id, CancellationToken ct = default);
}

public interface ITournamentRepository : IRepository<Tournament>
{
    Task<Tournament?> GetWithDetailsAsync(int id, CancellationToken ct = default);

    Task<IReadOnlyList<Tournament>> ListWithSportAsync(CancellationToken ct = default);
}

public interface ITeamRepository : IRepository<Team>
{
    /// <summary>Teams of one tournament, with their won players loaded for the standings panel.</summary>
    Task<IReadOnlyList<Team>> ListForTournamentAsync(int tournamentId, CancellationToken ct = default);

    /// <summary>Teams the given owner bids for, newest tournament first.</summary>
    Task<IReadOnlyList<Team>> ListForOwnerAsync(int ownerUserId, CancellationToken ct = default);

    Task<Team?> GetWithSquadAsync(int teamId, CancellationToken ct = default);

    /// <summary>Restores every purse in the tournament to its starting value.</summary>
    Task<int> ResetPursesAsync(int tournamentId, CancellationToken ct = default);
}

public interface IPlayerRepository : IRepository<Player>
{
    Task<Player?> GetWithDetailsAsync(int id, CancellationToken ct = default);

    Task<IReadOnlyList<Player>> ListForTournamentAsync(int tournamentId, CancellationToken ct = default);

    /// <summary>Cheapest base price still available to be bought, used by the purse-reserve rule.</summary>
    Task<decimal?> CheapestAvailableBaseAsync(int tournamentId, Gender? gender, CancellationToken ct = default);

    /// <summary>Players stamped with this auction — everything it sold or passed. Tracked.</summary>
    Task<IReadOnlyList<Player>> ListForAuctionAsync(int auctionId, CancellationToken ct = default);

    /// <summary>Clears sale results for a tournament so its auction can be re-run from draft.</summary>
    Task<int> ResetPoolAsync(int tournamentId, CancellationToken ct = default);
}

public interface IAuctionRepository : IRepository<Auction>
{
    Task<Auction?> GetWithDetailsAsync(int id, CancellationToken ct = default);

    Task<IReadOnlyList<Auction>> ListWithTournamentAsync(CancellationToken ct = default);

    /// <summary>
    /// Every <em>public</em> auction currently accepting bids or paused — the lobby feed.
    /// Private rooms are deliberately absent.
    /// </summary>
    Task<IReadOnlyList<Auction>> ListRunningAsync(CancellationToken ct = default);

    /// <summary>Resolves a room from its share link. The only lookup spectators can perform.</summary>
    Task<Auction?> GetByPublicIdAsync(Guid publicId, CancellationToken ct = default);

    /// <summary>Drafts with a start date still ahead of them, soonest first.</summary>
    Task<IReadOnlyList<Auction>> ListUpcomingAsync(CancellationToken ct = default);

    /// <summary>Completed auctions whose end date is older than the retention window.</summary>
    Task<IReadOnlyList<Auction>> ListExpiredAsync(DateTime endedBefore, CancellationToken ct = default);

    /// <summary>Ids of live auctions whose lot timer has run out. Ids only — the caller re-reads under lock.</summary>
    Task<IReadOnlyList<int>> ListTimedOutLotsAsync(DateTime asOf, CancellationToken ct = default);

    /// <summary>The running auction for one tournament, if any. Two cannot run at once per tournament.</summary>
    Task<Auction?> GetRunningForTournamentAsync(int tournamentId, CancellationToken ct = default);
}

public interface IBidRepository : IRepository<Bid>
{
    /// <summary>Highest bid standing against a player, with the team loaded.</summary>
    Task<Bid?> GetTopBidAsync(int auctionId, int playerId, CancellationToken ct = default);

    /// <summary>The same bid, tracked, so the auctioneer can retract it.</summary>
    Task<Bid?> GetTopBidForUpdateAsync(int auctionId, int playerId, CancellationToken ct = default);

    Task<IReadOnlyList<Bid>> ListRecentAsync(int auctionId, int take, CancellationToken ct = default);

    Task<IReadOnlyList<Bid>> ListForPlayerAsync(int auctionId, int playerId, CancellationToken ct = default);

    Task<Bid?> FindByClientRequestIdAsync(int auctionId, string clientRequestId, CancellationToken ct = default);

    /// <summary>
    /// Clears a player's bid history. Bids reference the player with no cascade, so they
    /// have to go before the player row can be removed.
    /// </summary>
    Task<int> DeleteForPlayerAsync(int playerId, CancellationToken ct = default);
}

public interface IUserRepository : IRepository<User>
{
    Task<User?> GetByUsernameAsync(string username, CancellationToken ct = default);

    Task<bool> UsernameExistsAsync(string username, CancellationToken ct = default);

    Task<IReadOnlyList<User>> ListByRoleAsync(UserRole role, CancellationToken ct = default);
}

/// <summary>
/// Groups the repositories behind one save boundary so a multi-entity change —
/// selling a player debits a purse and stamps the player in the same commit — is atomic.
/// </summary>
public interface IUnitOfWork
{
    ISportRepository Sports { get; }

    ITournamentRepository Tournaments { get; }

    ITeamRepository Teams { get; }

    IPlayerRepository Players { get; }

    IAuctionRepository Auctions { get; }

    IBidRepository Bids { get; }

    IUserRepository Users { get; }

    Task<int> SaveChangesAsync(CancellationToken ct = default);

    /// <summary>
    /// Runs <paramref name="operation"/> inside a transaction that commits as one unit.
    /// </summary>
    /// <remarks>
    /// The connection retries on transient faults, and that retry strategy refuses to
    /// resume a transaction it did not open — so the whole block, not just the save, has
    /// to be handed to it. A retry re-invokes <paramref name="operation"/>, so the body
    /// must assign absolute values rather than adjust them relative to what is already there.
    /// </remarks>
    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, CancellationToken ct = default);
}
