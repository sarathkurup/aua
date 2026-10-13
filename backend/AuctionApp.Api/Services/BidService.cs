using AuctionApp.Api.Contracts;
using AuctionApp.Api.Data.Repositories;
using AuctionApp.Api.Domain;
using AuctionApp.Api.Domain.Entities;

namespace AuctionApp.Api.Services;

public interface IBidService
{
    /// <summary>A team owner bidding for one of their own teams.</summary>
    Task<BidDto> PlaceForOwnerAsync(int auctionId, int userId, PlaceBidRequest request, CancellationToken ct = default);

    /// <summary>
    /// An admin entering a bid on a team's behalf — the offline path, for a bidder in the
    /// room whose device cannot reach the app.
    /// </summary>
    Task<BidDto> PlaceManualAsync(int auctionId, int adminUserId, ManualBidRequest request, CancellationToken ct = default);

    /// <summary>
    /// Takes back the standing bid on the lot currently up. The correction for the most
    /// common mistake in a room run by hand: the auctioneer taps the wrong team.
    /// </summary>
    Task<AuctionStateDto> UndoLastBidAsync(int auctionId, int adminUserId, CancellationToken ct = default);

    Task<IReadOnlyList<BidDto>> ListForPlayerAsync(int auctionId, int playerId, CancellationToken ct = default);

    Task<IReadOnlyList<BidDto>> ListRecentAsync(int auctionId, int take, CancellationToken ct = default);
}

public class BidService : IBidService
{
    private readonly IUnitOfWork _uow;
    private readonly IAuctionService _auctions;
    private readonly IAuctionNotifier _notifier;
    private readonly IAuctionLockProvider _locks;
    private readonly ISquadRuleService _rules;
    private readonly ILogger<BidService> _logger;

    public BidService(
        IUnitOfWork uow,
        IAuctionService auctions,
        IAuctionNotifier notifier,
        IAuctionLockProvider locks,
        ISquadRuleService rules,
        ILogger<BidService> logger)
    {
        _uow = uow;
        _auctions = auctions;
        _notifier = notifier;
        _locks = locks;
        _rules = rules;
        _logger = logger;
    }

    public async Task<BidDto> PlaceForOwnerAsync(
        int auctionId,
        int userId,
        PlaceBidRequest request,
        CancellationToken ct = default)
    {
        var auction = await _uow.Auctions.GetWithDetailsAsync(auctionId, ct)
            ?? throw new NotFoundException($"Auction {auctionId} was not found.");

        var owned = (await _uow.Teams.ListForOwnerAsync(userId, ct))
            .Where(t => t.TournamentId == auction.TournamentId)
            .ToList();

        if (owned.Count == 0)
        {
            throw new AuctionRuleException("You do not own a team in this tournament.");
        }

        Team team;
        if (request.TeamId is int requestedId)
        {
            team = owned.FirstOrDefault(t => t.Id == requestedId)
                ?? throw new AuctionRuleException("That team is not yours to bid for.");
        }
        else if (owned.Count == 1)
        {
            team = owned[0];
        }
        else
        {
            throw new AuctionRuleException("Select which team you are bidding for.");
        }

        return await PlaceAsync(auctionId, team.Id, request, userId, isManualOverride: false, ct);
    }

    public async Task<BidDto> PlaceManualAsync(
        int auctionId,
        int adminUserId,
        ManualBidRequest request,
        CancellationToken ct = default)
    {
        var bid = new PlaceBidRequest
        {
            PlayerId = request.PlayerId,
            TeamId = request.TeamId,
            Amount = request.Amount,
        };

        return await PlaceAsync(auctionId, request.TeamId, bid, adminUserId, isManualOverride: true, ct);
    }

    private async Task<BidDto> PlaceAsync(
        int auctionId,
        int teamId,
        PlaceBidRequest request,
        int? placedByUserId,
        bool isManualOverride,
        CancellationToken ct)
    {
        // Locks are per auction, so two rooms running at once never wait on each other.
        await using var gate = await _locks.AcquireAsync(auctionId, ct);

        // A client that lost its connection mid-bid retries with the same key. Return
        // the bid it already made rather than stacking a second one on top.
        if (!string.IsNullOrWhiteSpace(request.ClientRequestId))
        {
            var replay = await _uow.Bids.FindByClientRequestIdAsync(auctionId, request.ClientRequestId, ct);
            if (replay is not null)
            {
                _logger.LogInformation(
                    "Replayed bid {BidId} for client request {ClientRequestId}",
                    replay.Id,
                    request.ClientRequestId);
                return replay.ToDto();
            }
        }

        var auction = await _uow.Auctions.GetWithDetailsAsync(auctionId, ct)
            ?? throw new NotFoundException($"Auction {auctionId} was not found.");

        if (auction.Status != AuctionStatus.Live)
        {
            throw new AuctionRuleException(auction.Status == AuctionStatus.Paused
                ? "The auction is paused."
                : "The auction is not accepting bids.");
        }

        if (auction.CurrentPlayerId is null)
        {
            throw new AuctionRuleException("No player is on the block yet.");
        }

        if (auction.CurrentPlayerId != request.PlayerId)
        {
            throw new AuctionRuleException("That player is no longer on the block.");
        }

        var player = await _uow.Players.GetWithDetailsAsync(request.PlayerId, ct)
            ?? throw new NotFoundException($"Player {request.PlayerId} was not found.");

        if (player.Status != PlayerStatus.OnBlock)
        {
            throw new AuctionRuleException($"{player.Name} is not currently up for bidding.");
        }

        var team = await _uow.Teams.GetWithSquadAsync(teamId, ct)
            ?? throw new NotFoundException($"Team {teamId} was not found.");

        if (team.TournamentId != auction.TournamentId)
        {
            throw new AuctionRuleException($"{team.Name} is not part of this tournament.");
        }

        if (!team.IsActive)
        {
            throw new AuctionRuleException($"{team.Name} is not an active team.");
        }

        var topBid = await _uow.Bids.GetTopBidAsync(auctionId, request.PlayerId, ct);

        if (topBid is not null && topBid.TeamId == teamId)
        {
            throw new AuctionRuleException($"{team.Name} already holds the top bid.");
        }

        // The step widens as the price climbs, so a ₹1,000 raise on an ₹80,000 lot is
        // refused if the ladder says that range moves in ₹5,000s.
        var ladder = BidLadder.Parse(auction.IncrementRulesJson, auction.MinBidIncrement);
        var minimum = BidLadder.NextMinimum(
            ladder,
            AuctionService.OpeningPrice(auction, player),
            topBid?.Amount);

        if (request.Amount < minimum)
        {
            throw new AuctionRuleException($"Bid must be at least {minimum:N0}.");
        }

        var tournament = auction.Tournament
            ?? throw new NotFoundException($"Tournament {auction.TournamentId} was not found.");

        // Purse, squad size, per-player cap and the composition reserve all live here.
        await _rules.EnsureBidAllowedAsync(tournament, team, player, request.Amount, ct);

        var bid = new Bid
        {
            AuctionId = auctionId,
            PlayerId = player.Id,
            TeamId = team.Id,
            Amount = request.Amount,
            PlacedAt = DateTime.UtcNow,
            IsManualOverride = isManualOverride,
            PlacedByUserId = placedByUserId,
            ClientRequestId = string.IsNullOrWhiteSpace(request.ClientRequestId) ? null : request.ClientRequestId,
        };

        await _uow.Bids.AddAsync(bid, ct);

        // Every bid buys the lot more time — otherwise a raise at the death would still be
        // hammered down a second later, which is not how a call works.
        auction.LotDeadlineAt = AuctionService.NewDeadline(auction);

        await _uow.SaveChangesAsync(ct);

        var dto = new BidDto(
            bid.Id,
            auctionId,
            player.Id,
            player.Name,
            team.Id,
            team.Name,
            team.ShortName,
            bid.Amount,
            bid.PlacedAt,
            bid.IsManualOverride);

        _logger.LogInformation(
            "Bid {BidId}: {Amount} on player {PlayerId} by team {TeamId} (override: {IsOverride})",
            bid.Id,
            bid.Amount,
            player.Id,
            team.Id,
            bid.IsManualOverride);

        await _notifier.BidPlacedAsync(auctionId, dto, ct);

        var state = await _auctions.GetStateAsync(auctionId, ct);
        if (state is not null)
        {
            await _notifier.StateChangedAsync(state, ct);
        }

        // Lobby cards show the standing bid, so they move with it.
        await _notifier.LobbyChangedAsync(await _auctions.ListRunningAsync(ct), ct);

        return dto;
    }

    public async Task<AuctionStateDto> UndoLastBidAsync(
        int auctionId,
        int adminUserId,
        CancellationToken ct = default)
    {
        // Same gate the bid path takes: a bid landing while this runs would leave the
        // auctioneer retracting a different bid than the one they saw.
        await using var gate = await _locks.AcquireAsync(auctionId, ct);

        var auction = await _uow.Auctions.GetWithDetailsAsync(auctionId, ct)
            ?? throw new NotFoundException($"Auction {auctionId} was not found.");

        if (auction.Status is not (AuctionStatus.Live or AuctionStatus.Paused))
        {
            throw new AuctionRuleException("The auction is not running.");
        }

        // Scoped to the open lot on purpose. Reaching back into a resolved lot would
        // change a price a team has already been charged; reopening it is the way to do that.
        var playerId = auction.CurrentPlayerId
            ?? throw new AuctionRuleException(
                "No lot is on the block. To correct a sale that has already gone through, reopen it.");

        var bid = await _uow.Bids.GetTopBidForUpdateAsync(auctionId, playerId, ct)
            ?? throw new AuctionRuleException("There is no bid on this lot to take back.");

        bid.RetractedAt = DateTime.UtcNow;
        bid.RetractedByUserId = adminUserId;
        await _uow.SaveChangesAsync(ct);

        _logger.LogWarning(
            "Bid {BidId} retracted by user {UserId}: {Amount} on player {PlayerId} for team {TeamId}",
            bid.Id,
            adminUserId,
            bid.Amount,
            bid.PlayerId,
            bid.TeamId);

        var state = await _auctions.GetStateAsync(auctionId, ct)
            ?? throw new NotFoundException($"Auction {auctionId} was not found.");

        await _notifier.StateChangedAsync(state, ct);
        await _notifier.LobbyChangedAsync(await _auctions.ListRunningAsync(ct), ct);

        return state;
    }

    public async Task<IReadOnlyList<BidDto>> ListForPlayerAsync(int auctionId, int playerId, CancellationToken ct = default)
    {
        var bids = await _uow.Bids.ListForPlayerAsync(auctionId, playerId, ct);
        return bids.Select(b => b.ToDto()).ToList();
    }

    public async Task<IReadOnlyList<BidDto>> ListRecentAsync(int auctionId, int take, CancellationToken ct = default)
    {
        var bids = await _uow.Bids.ListRecentAsync(auctionId, Math.Clamp(take, 1, 100), ct);
        return bids.Select(b => b.ToDto()).ToList();
    }
}
