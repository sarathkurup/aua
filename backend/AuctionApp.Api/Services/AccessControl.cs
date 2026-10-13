using AuctionApp.Api.Data.Repositories;
using AuctionApp.Api.Domain;
using AuctionApp.Api.Domain.Entities;
using AuctionApp.Api.Infrastructure;

namespace AuctionApp.Api.Services;

/// <summary>The account behind the request being handled, if any.</summary>
public interface ICurrentUser
{
    int? UserId { get; }

    bool IsAdmin { get; }

    bool IsAuthenticated { get; }
}

public class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _accessor;

    public CurrentUser(IHttpContextAccessor accessor) => _accessor = accessor;

    public int? UserId
    {
        get
        {
            var principal = _accessor.HttpContext?.User;
            if (principal?.Identity?.IsAuthenticated != true)
            {
                return null;
            }

            return principal.TryGetUserId(out var id) ? id : null;
        }
    }

    public bool IsAdmin => _accessor.HttpContext?.User.IsInRole(nameof(UserRole.Admin)) == true;

    public bool IsAuthenticated => _accessor.HttpContext?.User.Identity?.IsAuthenticated == true;
}

/// <summary>
/// Decides who may run a tournament.
///
/// Anyone signed in can create one, and creating it makes them its auctioneer. From then on
/// only that owner — or a site admin — may touch its rules, pool, teams or auctions. The
/// check lives in the service layer rather than on controller attributes because it depends
/// on which row is being addressed, which an attribute cannot see.
/// </summary>
public interface ITournamentAccess
{
    /// <summary>True when the caller may manage this tournament.</summary>
    bool CanManage(Tournament tournament);

    /// <summary>Throws unless the caller may manage the tournament.</summary>
    Task RequireManageAsync(int tournamentId, CancellationToken ct = default);

    /// <summary>Throws unless the caller may manage the tournament this auction belongs to.</summary>
    Task RequireManageAuctionAsync(int auctionId, CancellationToken ct = default);

    /// <summary>Throws unless the caller may manage the tournament this team belongs to.</summary>
    Task RequireManageTeamAsync(int teamId, CancellationToken ct = default);

    /// <summary>Throws unless the caller may manage the tournament this player belongs to.</summary>
    Task RequireManagePlayerAsync(int playerId, CancellationToken ct = default);

    /// <summary>The account to record as owner for something being created now.</summary>
    int RequireUserId();
}

public class TournamentAccess : ITournamentAccess
{
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _uow;

    public TournamentAccess(ICurrentUser currentUser, IUnitOfWork uow)
    {
        _currentUser = currentUser;
        _uow = uow;
    }

    public bool CanManage(Tournament tournament)
    {
        if (_currentUser.IsAdmin)
        {
            return true;
        }

        var userId = _currentUser.UserId;
        return userId is not null && tournament.OwnerUserId == userId;
    }

    public async Task RequireManageAsync(int tournamentId, CancellationToken ct = default)
    {
        var tournament = await _uow.Tournaments.GetByIdAsync(tournamentId, ct)
            ?? throw new NotFoundException($"Tournament {tournamentId} was not found.");

        Require(tournament);
    }

    public async Task RequireManageAuctionAsync(int auctionId, CancellationToken ct = default)
    {
        var auction = await _uow.Auctions.GetByIdAsync(auctionId, ct)
            ?? throw new NotFoundException($"Auction {auctionId} was not found.");

        await RequireManageAsync(auction.TournamentId, ct);
    }

    public async Task RequireManageTeamAsync(int teamId, CancellationToken ct = default)
    {
        var team = await _uow.Teams.GetByIdAsync(teamId, ct)
            ?? throw new NotFoundException($"Team {teamId} was not found.");

        await RequireManageAsync(team.TournamentId, ct);
    }

    public async Task RequireManagePlayerAsync(int playerId, CancellationToken ct = default)
    {
        var player = await _uow.Players.GetByIdAsync(playerId, ct)
            ?? throw new NotFoundException($"Player {playerId} was not found.");

        await RequireManageAsync(player.TournamentId, ct);
    }

    public int RequireUserId() =>
        _currentUser.UserId
        ?? throw new UnauthorizedAccessException("This action needs a signed-in account.");

    private void Require(Tournament tournament)
    {
        if (!CanManage(tournament))
        {
            // Deliberately not "not found": the tournament is public, only control of it
            // is restricted, so saying so is more useful than pretending it is missing.
            throw new ForbiddenException(
                $"Only the auctioneer who created '{tournament.Name}' can change it.");
        }
    }
}
