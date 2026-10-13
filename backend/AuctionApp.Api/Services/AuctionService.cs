using AuctionApp.Api.Contracts;
using AuctionApp.Api.Data.Repositories;
using AuctionApp.Api.Domain;
using AuctionApp.Api.Domain.Entities;

namespace AuctionApp.Api.Services;

public interface IAuctionService
{
    Task<IReadOnlyList<AuctionDto>> ListAsync(CancellationToken ct = default);

    Task<AuctionDto> CreateAsync(CreateAuctionRequest request, CancellationToken ct = default);

    Task<AuctionStateDto?> GetStateAsync(int auctionId, CancellationToken ct = default);

    /// <summary>
    /// Resolves a room from its share link. This is the only way an account-less spectator
    /// can reach an auction, and it works for public and private rooms alike — holding the
    /// link is the permission.
    /// </summary>
    Task<AuctionStateDto?> GetStateByLinkAsync(Guid publicId, CancellationToken ct = default);

    /// <summary>Every auction currently running — the public lobby feed.</summary>
    Task<IReadOnlyList<LiveAuctionCardDto>> ListRunningAsync(CancellationToken ct = default);

    /// <summary>
    /// Scheduled auctions that have not opened yet. Public ones are shown to everyone;
    /// private ones only to the auctioneer who owns them.
    /// </summary>
    Task<IReadOnlyList<LiveAuctionCardDto>> ListUpcomingAsync(
        int? viewerUserId,
        bool viewerIsAdmin,
        CancellationToken ct = default);

    /// <summary>Edits an auction. Allowed at any point in its life, including while it runs.</summary>
    Task<AuctionDto> UpdateAsync(int auctionId, SaveAuctionRequest request, CancellationToken ct = default);

    Task<AuctionStateDto> StartAsync(int auctionId, CancellationToken ct = default);

    Task<AuctionStateDto> PauseAsync(int auctionId, CancellationToken ct = default);

    Task<AuctionStateDto> ResumeAsync(int auctionId, CancellationToken ct = default);

    Task<AuctionStateDto> EndAsync(int auctionId, CancellationToken ct = default);

    Task<AuctionStateDto> PutPlayerOnBlockAsync(int auctionId, int playerId, CancellationToken ct = default);

    Task<AuctionStateDto> SellCurrentPlayerAsync(int auctionId, CancellationToken ct = default);

    Task<AuctionStateDto> MarkCurrentPlayerUnsoldAsync(int auctionId, CancellationToken ct = default);

    /// <summary>
    /// Puts the most recently resolved lot back on the block and refunds what was paid for
    /// it. The recovery for a hammer that fell on the wrong team.
    /// </summary>
    Task<AuctionStateDto> ReopenLastLotAsync(int auctionId, CancellationToken ct = default);

    Task DeleteAsync(int auctionId, CancellationToken ct = default);
}

public class AuctionService : IAuctionService
{
    private const int RecentBidCount = 15;

    private readonly IUnitOfWork _uow;
    private readonly IAuctionNotifier _notifier;
    private readonly IAuctionLockProvider _locks;
    private readonly ISquadRuleService _rules;
    private readonly ILogger<AuctionService> _logger;

    public AuctionService(
        IUnitOfWork uow,
        IAuctionNotifier notifier,
        IAuctionLockProvider locks,
        ISquadRuleService rules,
        ILogger<AuctionService> logger)
    {
        _uow = uow;
        _notifier = notifier;
        _locks = locks;
        _rules = rules;
        _logger = logger;
    }

    public async Task<IReadOnlyList<AuctionDto>> ListAsync(CancellationToken ct = default)
    {
        var auctions = await _uow.Auctions.ListWithTournamentAsync(ct);
        return auctions.Select(a => a.ToDto()).ToList();
    }

    public async Task<AuctionDto> CreateAsync(CreateAuctionRequest request, CancellationToken ct = default)
    {
        var tournament = await _uow.Tournaments.GetWithDetailsAsync(request.TournamentId, ct)
            ?? throw new NotFoundException($"Tournament {request.TournamentId} was not found.");

        var auction = new Auction
        {
            Name = request.Name.Trim(),
            TournamentId = tournament.Id,
            MinBidIncrement = request.MinBidIncrement,
            MinimumBid = request.MinimumBid,
            IncrementRulesJson = BidLadder.Serialise(request.Increments),
            AutoTimeoutSeconds = request.AutoTimeoutSeconds,
            Status = AuctionStatus.Draft,
            Visibility = request.Visibility,
            PublicId = Guid.NewGuid(),
            ScheduledStartAt = ValidateSchedule(request.ScheduledStartAt),
        };

        await _uow.Auctions.AddAsync(auction, ct);
        await _uow.SaveChangesAsync(ct);

        var created = await _uow.Auctions.GetWithDetailsAsync(auction.Id, ct);
        return created!.ToDto();
    }

    // Read-only: deliberately does not take the auction lock, because it is called
    // from inside lock-holding write paths to build the broadcast payload.
    public async Task<AuctionStateDto?> GetStateAsync(int auctionId, CancellationToken ct = default)
    {
        var auction = await _uow.Auctions.GetWithDetailsAsync(auctionId, ct);
        return auction is null ? null : await BuildStateAsync(auction, ct);
    }

    public async Task<AuctionStateDto?> GetStateByLinkAsync(Guid publicId, CancellationToken ct = default)
    {
        var auction = await _uow.Auctions.GetByPublicIdAsync(publicId, ct);
        return auction is null ? null : await BuildStateAsync(auction, ct);
    }

    public async Task<IReadOnlyList<LiveAuctionCardDto>> ListRunningAsync(CancellationToken ct = default) =>
        await BuildCardsAsync(await _uow.Auctions.ListRunningAsync(ct), ct);

    public async Task<IReadOnlyList<LiveAuctionCardDto>> ListUpcomingAsync(
        int? viewerUserId,
        bool viewerIsAdmin,
        CancellationToken ct = default)
    {
        var upcoming = await _uow.Auctions.ListUpcomingAsync(ct);

        // Public schedules are advertised to everyone. A private one appears only for the
        // auctioneer who owns it — the unlisted promise holds before the room opens, not
        // just once it is live.
        var visible = upcoming
            .Where(a => a.Visibility == AuctionVisibility.Public
                        || viewerIsAdmin
                        || (viewerUserId is not null && a.Tournament?.OwnerUserId == viewerUserId))
            .ToList();

        return await BuildCardsAsync(visible, ct);
    }

    private async Task<IReadOnlyList<LiveAuctionCardDto>> BuildCardsAsync(
        IReadOnlyList<Auction> auctions,
        CancellationToken ct)
    {
        var cards = new List<LiveAuctionCardDto>(auctions.Count);

        foreach (var auction in auctions)
        {
            var teams = await _uow.Teams.ListForTournamentAsync(auction.TournamentId, ct);
            var players = await _uow.Players.ListForTournamentAsync(auction.TournamentId, ct);

            Bid? topBid = null;
            Player? current = null;
            if (auction.CurrentPlayerId is int currentId)
            {
                current = players.FirstOrDefault(p => p.Id == currentId);
                topBid = await _uow.Bids.GetTopBidAsync(auction.Id, currentId, ct);
            }

            cards.Add(new LiveAuctionCardDto(
                auction.PublicId,
                auction.Visibility,
                auction.ScheduledStartAt,
                auction.Name,
                auction.Tournament?.Name ?? string.Empty,
                auction.Tournament?.Sport?.Name ?? string.Empty,
                auction.Tournament?.Sport?.Icon,
                auction.Tournament?.Type ?? TournamentType.Professional,
                auction.Status,
                current?.Name,
                current?.ToDto().PhotoThumbUrl,
                topBid?.Amount,
                topBid?.Team?.Name,
                teams.Count,
                players.Count(p => p.Status == PlayerStatus.Sold),
                players.Count));
        }

        return cards;
    }

    public async Task<AuctionDto> UpdateAsync(int auctionId, SaveAuctionRequest request, CancellationToken ct = default)
    {
        var auction = await RequireAuctionAsync(auctionId, ct);

        // Deliberately unguarded by status: the auctioneer can rename a room, change its
        // increment or flip it to unlisted while it is running.
        auction.Name = request.Name.Trim();
        auction.MinBidIncrement = request.MinBidIncrement;
        auction.MinimumBid = request.MinimumBid;
        auction.IncrementRulesJson = BidLadder.Serialise(request.Increments);
        auction.Visibility = request.Visibility;
        auction.ScheduledStartAt = ValidateSchedule(request.ScheduledStartAt);

        // Changing the timer mid-lot re-arms the lot on the block against the new duration,
        // rather than leaving it counting down to a deadline set under the old one.
        var timerChanged = auction.AutoTimeoutSeconds != request.AutoTimeoutSeconds;
        auction.AutoTimeoutSeconds = request.AutoTimeoutSeconds;
        if (timerChanged && auction.CurrentPlayerId is not null)
        {
            auction.LotDeadlineAt = NewDeadline(auction);
        }

        await _uow.SaveChangesAsync(ct);
        _logger.LogInformation("Auction {AuctionId} updated", auctionId);

        var fresh = await _uow.Auctions.GetWithDetailsAsync(auctionId, ct) ?? auction;

        // A visibility or name change alters what the dashboard should be showing.
        if (fresh.Status is AuctionStatus.Live or AuctionStatus.Paused)
        {
            await PublishAsync(fresh, ct, statusChanged: true);
        }
        else
        {
            await _notifier.LobbyChangedAsync(await ListRunningAsync(ct), ct);
        }

        return fresh.ToDto();
    }

    /// <summary>
    /// A schedule may sit up to <see cref="AuctionSchedule.MaxDaysAhead"/> days out. Times in
    /// the past are accepted and simply mean the room is already due to open.
    /// </summary>
    private static DateTime? ValidateSchedule(DateTime? scheduledStartAt)
    {
        if (scheduledStartAt is not DateTime when)
        {
            return null;
        }

        var utc = when.Kind == DateTimeKind.Utc ? when : when.ToUniversalTime();

        if (utc > DateTime.UtcNow.AddDays(AuctionSchedule.MaxDaysAhead))
        {
            throw new AuctionRuleException(
                $"An auction can only be scheduled up to {AuctionSchedule.MaxDaysAhead} days ahead.");
        }

        return utc;
    }

    public async Task<AuctionStateDto> StartAsync(int auctionId, CancellationToken ct = default)
    {
        var auction = await RequireAuctionAsync(auctionId, ct);

        // Scheduled rooms open on their date, not before. Clearing or moving the schedule
        // is an edit the auctioneer can make at any time, so this is never a dead end.
        if (auction.Status == AuctionStatus.Draft &&
            auction.ScheduledStartAt is DateTime due &&
            due > DateTime.UtcNow)
        {
            throw new AuctionRuleException(
                $"This auction is scheduled for {due:dd MMM yyyy HH:mm} UTC. " +
                "Change the schedule if you want to open it now.");
        }

        if (auction.Status == AuctionStatus.Live)
        {
            throw new AuctionRuleException("This auction is already live.");
        }

        if (auction.Status == AuctionStatus.Completed)
        {
            throw new AuctionRuleException("This auction has finished. Create a new one to run it again.");
        }

        // Auctions in different tournaments run side by side. Two inside one tournament
        // would spend the same purses against the same pool, so that stays blocked.
        var sibling = await _uow.Auctions.GetRunningForTournamentAsync(auction.TournamentId, ct);
        if (sibling is not null && sibling.Id != auctionId)
        {
            throw new AuctionRuleException(
                $"'{sibling.Name}' is already running for this tournament. End it before starting another.");
        }

        // A draft start is a fresh run: clear prior sale results and restore purses.
        if (auction.Status == AuctionStatus.Draft)
        {
            await _uow.Players.ResetPoolAsync(auction.TournamentId, ct);
            await _uow.Teams.ResetPursesAsync(auction.TournamentId, ct);
        }

        auction.Status = AuctionStatus.Live;
        auction.StartedAt ??= DateTime.UtcNow;
        auction.CurrentPlayerId = null;
        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation("Auction {AuctionId} started", auctionId);
        return await PublishAsync(auction, ct, statusChanged: true);
    }

    public async Task<AuctionStateDto> PauseAsync(int auctionId, CancellationToken ct = default)
    {
        var auction = await RequireAuctionAsync(auctionId, ct);

        if (auction.Status != AuctionStatus.Live)
        {
            throw new AuctionRuleException("Only a live auction can be paused.");
        }

        auction.Status = AuctionStatus.Paused;
        await _uow.SaveChangesAsync(ct);
        return await PublishAsync(auction, ct, statusChanged: true);
    }

    public async Task<AuctionStateDto> ResumeAsync(int auctionId, CancellationToken ct = default)
    {
        var auction = await RequireAuctionAsync(auctionId, ct);

        if (auction.Status != AuctionStatus.Paused)
        {
            throw new AuctionRuleException("Only a paused auction can be resumed.");
        }

        auction.Status = AuctionStatus.Live;
        await _uow.SaveChangesAsync(ct);
        return await PublishAsync(auction, ct, statusChanged: true);
    }

    public async Task<AuctionStateDto> EndAsync(int auctionId, CancellationToken ct = default)
    {
        await using var gate = await _locks.AcquireAsync(auctionId, ct);
        var auction = await RequireAuctionAsync(auctionId, ct);

        if (auction.Status is not (AuctionStatus.Live or AuctionStatus.Paused))
        {
            throw new AuctionRuleException("Only a running auction can be closed.");
        }

        // Whoever is on the block when the hammer stops goes down as unsold.
        if (auction.CurrentPlayerId is not null)
        {
            var onBlock = await _uow.Players.GetByIdAsync(auction.CurrentPlayerId.Value, ct);
            if (onBlock is not null && onBlock.Status == PlayerStatus.OnBlock)
            {
                onBlock.Status = PlayerStatus.Unsold;
                onBlock.AuctionId = auction.Id;
            }
        }

        auction.CurrentPlayerId = null;
        auction.LotDeadlineAt = null;
        auction.Status = AuctionStatus.Completed;
        auction.EndedAt = DateTime.UtcNow;
        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation("Auction {AuctionId} closed", auctionId);
        return await PublishAsync(auction, ct, statusChanged: true);
    }

    public async Task<AuctionStateDto> PutPlayerOnBlockAsync(int auctionId, int playerId, CancellationToken ct = default)
    {
        await using var gate = await _locks.AcquireAsync(auctionId, ct);
        var auction = await RequireAuctionAsync(auctionId, ct);

        if (auction.Status != AuctionStatus.Live)
        {
            throw new AuctionRuleException("Start the auction before calling a player.");
        }

        if (auction.CurrentPlayerId is not null)
        {
            throw new AuctionRuleException("Sell or pass the current player before calling the next one.");
        }

        var player = await _uow.Players.GetWithDetailsAsync(playerId, ct)
            ?? throw new NotFoundException($"Player {playerId} was not found.");

        if (player.TournamentId != auction.TournamentId)
        {
            throw new AuctionRuleException($"{player.Name} is not in this tournament's pool.");
        }

        if (player.Status == PlayerStatus.Sold)
        {
            throw new AuctionRuleException($"{player.Name} has already been sold.");
        }

        if (player.Status == PlayerStatus.Retained)
        {
            throw new AuctionRuleException(
                $"{player.Name} is a retained member of {player.SoldToTeam?.Name ?? "their team"} " +
                "and is not part of the auction.");
        }

        player.Status = PlayerStatus.OnBlock;
        player.AuctionId = auction.Id;
        auction.CurrentPlayerId = player.Id;
        auction.LotDeadlineAt = NewDeadline(auction);
        await _uow.SaveChangesAsync(ct);

        return await PublishAsync(auction, ct);
    }

    public async Task<AuctionStateDto> SellCurrentPlayerAsync(int auctionId, CancellationToken ct = default)
    {
        await using var gate = await _locks.AcquireAsync(auctionId, ct);
        var auction = await RequireAuctionAsync(auctionId, ct);

        if (auction.Status is not (AuctionStatus.Live or AuctionStatus.Paused))
        {
            throw new AuctionRuleException("The auction is not running.");
        }

        var playerId = auction.CurrentPlayerId
            ?? throw new AuctionRuleException("No player is on the block.");

        var player = await _uow.Players.GetWithDetailsAsync(playerId, ct)
            ?? throw new NotFoundException($"Player {playerId} was not found.");

        var topBid = await _uow.Bids.GetTopBidAsync(auctionId, playerId, ct)
            ?? throw new AuctionRuleException($"No bids were placed for {player.Name}. Mark them unsold instead.");

        var winner = await _uow.Teams.GetWithSquadAsync(topBid.TeamId, ct)
            ?? throw new NotFoundException($"Team {topBid.TeamId} was not found.");

        var tournament = auction.Tournament
            ?? throw new NotFoundException($"Tournament {auction.TournamentId} was not found.");

        // Re-checked at the hammer, not just when the bid landed: the winning team may have
        // bought elsewhere in the meantime, and the squad rules must hold at the moment of sale.
        await _rules.EnsureBidAllowedAsync(tournament, winner, player, topBid.Amount, ct);

        // Computed once, outside the block: a transient-fault retry re-runs the body, and
        // a relative `-=` would debit the purse twice. The timestamp is fixed here for the
        // same reason — a retry must not move the sale later than it happened.
        var purseAfterSale = winner.Purse - topBid.Amount;
        var soldAt = DateTime.UtcNow;

        // Debiting the purse and stamping the sale must land together or not at all.
        await _uow.ExecuteInTransactionAsync(async token =>
        {
            player.Status = PlayerStatus.Sold;
            player.SoldPrice = topBid.Amount;
            player.SoldAt = soldAt;
            player.SoldToTeamId = winner.Id;
            player.AuctionId = auction.Id;
            winner.Purse = purseAfterSale;
            auction.CurrentPlayerId = null;
            auction.LotDeadlineAt = null;

            await _uow.SaveChangesAsync(token);
        },
        ct);

        _logger.LogInformation(
            "Player {PlayerId} sold to team {TeamId} for {Amount}",
            player.Id,
            winner.Id,
            topBid.Amount);

        var sold = await _uow.Players.GetWithDetailsAsync(player.Id, ct);
        await _notifier.PlayerSoldAsync(auctionId, sold!.ToDto(topBid), ct);
        return await PublishAsync(auction, ct);
    }

    public async Task<AuctionStateDto> MarkCurrentPlayerUnsoldAsync(int auctionId, CancellationToken ct = default)
    {
        await using var gate = await _locks.AcquireAsync(auctionId, ct);
        var auction = await RequireAuctionAsync(auctionId, ct);

        if (auction.Status is not (AuctionStatus.Live or AuctionStatus.Paused))
        {
            throw new AuctionRuleException("The auction is not running.");
        }

        var playerId = auction.CurrentPlayerId
            ?? throw new AuctionRuleException("No player is on the block.");

        var player = await _uow.Players.GetWithDetailsAsync(playerId, ct)
            ?? throw new NotFoundException($"Player {playerId} was not found.");

        player.Status = PlayerStatus.Unsold;
        player.AuctionId = auction.Id;
        auction.CurrentPlayerId = null;
        auction.LotDeadlineAt = null;
        await _uow.SaveChangesAsync(ct);

        await _notifier.PlayerUnsoldAsync(auctionId, player.ToDto(), ct);
        return await PublishAsync(auction, ct);
    }

    /// <summary>
    /// Puts the last sold lot back up and gives the money back.
    /// </summary>
    /// <remarks>
    /// Only sales are reversible here, because only a sale moves money and stamps a squad.
    /// A lot passed by mistake needs no special path — it goes back to the pool as unsold
    /// and the auctioneer simply calls it again.
    ///
    /// The player's bids are left standing, so the room comes back exactly as it was a
    /// moment before the hammer: the wrong team still leading, and the auctioneer one
    /// "undo last bid" away from the price it should have been.
    /// </remarks>
    public async Task<AuctionStateDto> ReopenLastLotAsync(int auctionId, CancellationToken ct = default)
    {
        await using var gate = await _locks.AcquireAsync(auctionId, ct);
        var auction = await RequireAuctionAsync(auctionId, ct);

        if (auction.Status is not (AuctionStatus.Live or AuctionStatus.Paused))
        {
            throw new AuctionRuleException("The auction is not running.");
        }

        if (auction.CurrentPlayerId is not null)
        {
            throw new AuctionRuleException(
                "A lot is already on the block. Sell or pass it before reopening the previous one.");
        }

        var players = await _uow.Players.ListForAuctionAsync(auctionId, ct);

        var player = players
            .Where(p => p.Status == PlayerStatus.Sold)
            .OrderByDescending(p => p.SoldAt ?? DateTime.MinValue)
            .ThenByDescending(p => p.Id)
            .FirstOrDefault()
            ?? throw new AuctionRuleException("Nothing has been sold in this auction yet.");

        var refund = player.SoldPrice ?? 0m;
        var buyerId = player.SoldToTeamId;

        Team? buyer = null;
        decimal purseAfterRefund = 0m;

        if (buyerId is int teamId)
        {
            buyer = await _uow.Teams.GetWithSquadAsync(teamId, ct)
                ?? throw new NotFoundException($"Team {teamId} was not found.");

            // Absolute, computed before the block: a transient-fault retry re-runs the body
            // and a relative `+=` would refund twice.
            purseAfterRefund = buyer.Purse + refund;
        }

        await _uow.ExecuteInTransactionAsync(async token =>
        {
            player.Status = PlayerStatus.OnBlock;
            player.SoldPrice = null;
            player.SoldAt = null;
            player.SoldToTeamId = null;
            player.AuctionId = auction.Id;

            if (buyer is not null) buyer.Purse = purseAfterRefund;

            auction.CurrentPlayerId = player.Id;
            auction.LotDeadlineAt = null;

            await _uow.SaveChangesAsync(token);
        },
        ct);

        _logger.LogWarning(
            "Lot reopened in auction {AuctionId}: player {PlayerId} un-sold from team {TeamId}, {Refund} refunded",
            auctionId,
            player.Id,
            buyerId,
            refund);

        return await PublishAsync(auction, ct);
    }

    /// <summary>
    /// Removes an auction at any point in its life, running or not.
    ///
    /// Deleting the event has to undo what it did, or the tournament is left with purses
    /// debited against a sale that no longer exists: every player it resolved goes back to
    /// the pool and every winning bid is refunded to the team that paid it.
    /// </summary>
    public async Task DeleteAsync(int auctionId, CancellationToken ct = default)
    {
        await using var gate = await _locks.AcquireAsync(auctionId, ct);
        var auction = await RequireAuctionAsync(auctionId, ct);
        var wasRunning = auction.Status is AuctionStatus.Live or AuctionStatus.Paused;

        var resolved = await _uow.Players.ListForAuctionAsync(auctionId, ct);

        // Refund targets are absolute values worked out once, because a transient-fault
        // retry re-runs the block and a relative credit would pay a team twice.
        var restoredPurses = new Dictionary<int, decimal>();
        foreach (var group in resolved
                     .Where(p => p.Status == PlayerStatus.Sold && p.SoldToTeamId is not null)
                     .GroupBy(p => p.SoldToTeamId!.Value))
        {
            var team = await _uow.Teams.GetByIdAsync(group.Key, ct);
            if (team is not null)
            {
                restoredPurses[group.Key] = team.Purse + group.Sum(p => p.SoldPrice ?? 0m);
            }
        }

        await _uow.ExecuteInTransactionAsync(async token =>
        {
            foreach (var (teamId, purse) in restoredPurses)
            {
                var team = await _uow.Teams.GetByIdAsync(teamId, token);
                if (team is not null)
                {
                    team.Purse = purse;
                }
            }

            foreach (var player in resolved)
            {
                player.Status = PlayerStatus.Available;
                player.SoldPrice = null;
                player.SoldAt = null;
                player.SoldToTeamId = null;
                player.AuctionId = null;
            }

            // Clear the block before the row goes, so nothing still points at a player.
            auction.CurrentPlayerId = null;
            await _uow.SaveChangesAsync(token);

            _uow.Auctions.Remove(auction);
            await _uow.SaveChangesAsync(token);
        },
        ct);

        _logger.LogInformation(
            "Auction {AuctionId} deleted; {PlayerCount} players returned to the pool, {TeamCount} purses refunded",
            auctionId,
            resolved.Count,
            restoredPurses.Count);

        if (wasRunning)
        {
            await _notifier.LobbyChangedAsync(await ListRunningAsync(ct), ct);
        }
    }

    private async Task<Auction> RequireAuctionAsync(int auctionId, CancellationToken ct) =>
        await _uow.Auctions.GetWithDetailsAsync(auctionId, ct)
        ?? throw new NotFoundException($"Auction {auctionId} was not found.");

    /// <summary>Rebuilds the room state, broadcasts it, and returns it to the caller.</summary>
    private async Task<AuctionStateDto> PublishAsync(Auction auction, CancellationToken ct, bool statusChanged = false)
    {
        // Re-read so the payload reflects committed state, including navigations
        // that changed during this operation.
        var fresh = await _uow.Auctions.GetWithDetailsAsync(auction.Id, ct) ?? auction;
        var state = await BuildStateAsync(fresh, ct);

        if (statusChanged)
        {
            await _notifier.AuctionStatusChangedAsync(state.Auction, ct);
        }

        await _notifier.StateChangedAsync(state, ct);
        await _notifier.LobbyChangedAsync(await ListRunningAsync(ct), ct);
        return state;
    }

    private async Task<AuctionStateDto> BuildStateAsync(Auction auction, CancellationToken ct)
    {
        Bid? topBid = null;
        Player? currentPlayer = null;

        if (auction.CurrentPlayerId is int currentId)
        {
            topBid = await _uow.Bids.GetTopBidAsync(auction.Id, currentId, ct);
            currentPlayer = await _uow.Players.GetWithDetailsAsync(currentId, ct);
        }

        var recent = await _uow.Bids.ListRecentAsync(auction.Id, RecentBidCount, ct);

        var tournament = auction.Tournament
            ?? await _uow.Tournaments.GetWithDetailsAsync(auction.TournamentId, ct)
            ?? throw new NotFoundException($"Tournament {auction.TournamentId} was not found.");

        var teams = await _rules.BuildStandingsAsync(tournament, currentPlayer, ct);

        return new AuctionStateDto(
            auction.ToDto(),
            tournament.ToRulesDto(),
            currentPlayer?.ToDto(topBid),
            topBid?.ToDto(),
            NextMinimumBid(auction, currentPlayer, topBid),
            recent.Select(b => b.ToDto()).ToList(),
            teams,
            await LastSaleAsync(auction.Id, ct));
    }

    /// <summary>
    /// The sale a reopen would put back. Read from the players stamped with this auction
    /// rather than from the bid feed, so it reflects what was actually charged.
    /// </summary>
    private async Task<LastSaleDto?> LastSaleAsync(int auctionId, CancellationToken ct)
    {
        var players = await _uow.Players.ListForAuctionAsync(auctionId, ct);

        var last = players
            .Where(p => p.Status == PlayerStatus.Sold)
            .OrderByDescending(p => p.SoldAt ?? DateTime.MinValue)
            .ThenByDescending(p => p.Id)
            .FirstOrDefault();

        if (last?.SoldToTeamId is not int teamId) return null;

        var team = await _uow.Teams.GetByIdAsync(teamId, ct);

        return new LastSaleDto(
            last.Id,
            last.Name,
            teamId,
            team?.Name ?? string.Empty,
            last.SoldPrice ?? 0m,
            last.SoldAt ?? DateTime.UtcNow);
    }

    /// <summary>
    /// What the next bid has to be: the opening price for an untouched lot, otherwise the
    /// standing bid plus whichever rung of the ladder applies at that price.
    /// </summary>
    private static decimal NextMinimumBid(Auction auction, Player? currentPlayer, Bid? topBid)
    {
        if (currentPlayer is null)
        {
            return 0m;
        }

        return BidLadder.NextMinimum(
            BidLadder.Parse(auction.IncrementRulesJson, auction.MinBidIncrement),
            OpeningPrice(auction, currentPlayer),
            topBid?.Amount);
    }

    /// <summary>
    /// A lot opens at its base price, but never below the auction's floor — that is what
    /// stops a ₹500 player being had for ₹500 in a room whose minimum is ₹1,000.
    /// </summary>
    public static decimal OpeningPrice(Auction auction, Player player) =>
        Math.Max(player.BasePrice, auction.MinimumBid);

    /// <summary>When the lot now on the block should expire, or null if the timer is off.</summary>
    public static DateTime? NewDeadline(Auction auction) =>
        auction.AutoTimeoutSeconds > 0
            ? DateTime.UtcNow.AddSeconds(auction.AutoTimeoutSeconds)
            : null;
}
