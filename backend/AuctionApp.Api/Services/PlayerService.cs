using System.Text.Json;
using AuctionApp.Api.Contracts;
using AuctionApp.Api.Data.Repositories;
using AuctionApp.Api.Domain;
using AuctionApp.Api.Domain.Entities;

namespace AuctionApp.Api.Services;

public interface IPlayerService
{
    Task<IReadOnlyList<PlayerDto>> ListAsync(int tournamentId, CancellationToken ct = default);

    Task<PlayerDto?> GetAsync(int id, CancellationToken ct = default);

    Task<PlayerDto> CreateAsync(SavePlayerRequest request, CancellationToken ct = default);

    Task<PlayerDto> UpdateAsync(int id, SavePlayerRequest request, CancellationToken ct = default);

    Task<PlayerDto> SetPhotoAsync(int id, Stream image, CancellationToken ct = default);

    Task DeleteAsync(int id, CancellationToken ct = default);
}

public class PlayerService : IPlayerService
{
    private readonly IUnitOfWork _uow;
    private readonly IPhotoService _photos;
    private readonly IAuctionService _auctions;
    private readonly IAuctionNotifier _notifier;

    public PlayerService(
        IUnitOfWork uow,
        IPhotoService photos,
        IAuctionService auctions,
        IAuctionNotifier notifier)
    {
        _uow = uow;
        _photos = photos;
        _auctions = auctions;
        _notifier = notifier;
    }

    public async Task<IReadOnlyList<PlayerDto>> ListAsync(int tournamentId, CancellationToken ct = default)
    {
        var players = await _uow.Players.ListForTournamentAsync(tournamentId, ct);
        return players.Select(p => p.ToDto()).ToList();
    }

    public async Task<PlayerDto?> GetAsync(int id, CancellationToken ct = default)
    {
        var player = await _uow.Players.GetWithDetailsAsync(id, ct);
        return player?.ToDto();
    }

    public async Task<PlayerDto> CreateAsync(SavePlayerRequest request, CancellationToken ct = default)
    {
        var tournament = await _uow.Tournaments.GetWithDetailsAsync(request.TournamentId, ct)
            ?? throw new NotFoundException($"Tournament {request.TournamentId} was not found.");

        var player = new Player
        {
            Name = request.Name.Trim(),
            TournamentId = tournament.Id,
            Role = request.Role?.Trim(),
            Gender = request.Gender,
            Country = request.Country?.Trim(),
            SkillsJson = SerialiseSkills(request.Skills),
            BasePrice = request.BasePrice,
            ImageUrl = string.IsNullOrWhiteSpace(request.ImageUrl) ? null : request.ImageUrl.Trim(),
            LotOrder = request.LotOrder,
            JerseyNumber = request.JerseyNumber,
            Status = PlayerStatus.Available,
        };

        await _uow.Players.AddAsync(player, ct);
        await _uow.SaveChangesAsync(ct);

        var created = await _uow.Players.GetWithDetailsAsync(player.Id, ct);
        return created!.ToDto();
    }

    /// <summary>
    /// Edits a player at any point, including one on the block or already sold — the
    /// auctioneer has to be able to fix a typo or a wrong base price mid-sale.
    /// A sale that already completed keeps its price; only the profile changes.
    /// </summary>
    public async Task<PlayerDto> UpdateAsync(int id, SavePlayerRequest request, CancellationToken ct = default)
    {
        var player = await _uow.Players.GetWithDetailsAsync(id, ct)
            ?? throw new NotFoundException($"Player {id} was not found.");

        // Moving a player between tournaments would carry them across pools and purses.
        if (request.TournamentId != 0 && request.TournamentId != player.TournamentId)
        {
            throw new AuctionRuleException("A player cannot be moved to a different tournament.");
        }

        player.Name = request.Name.Trim();
        player.Role = request.Role?.Trim();
        player.Gender = request.Gender;
        player.Country = request.Country?.Trim();
        player.SkillsJson = SerialiseSkills(request.Skills);
        player.BasePrice = request.BasePrice;
        player.ImageUrl = string.IsNullOrWhiteSpace(request.ImageUrl) ? null : request.ImageUrl.Trim();
        player.LotOrder = request.LotOrder;
        player.JerseyNumber = request.JerseyNumber;

        await _uow.SaveChangesAsync(ct);

        var updated = await _uow.Players.GetWithDetailsAsync(player.Id, ct);
        return updated!.ToDto();
    }

    public async Task<PlayerDto> SetPhotoAsync(int id, Stream image, CancellationToken ct = default)
    {
        var player = await _uow.Players.GetWithDetailsAsync(id, ct)
            ?? throw new NotFoundException($"Player {id} was not found.");

        var previousKey = player.PhotoKey;
        player.PhotoKey = await _photos.StoreAsync(image, ct);

        // The URL is derived from content, so an unchanged image keeps the same key and
        // there is nothing to clean up.
        if (previousKey is { Length: > 0 } && previousKey != player.PhotoKey)
        {
            _photos.Delete(previousKey);
        }

        await _uow.SaveChangesAsync(ct);

        var updated = await _uow.Players.GetWithDetailsAsync(player.Id, ct);
        return updated!.ToDto();
    }

    /// <summary>
    /// Removes a player at any point, including from a running auction.
    ///
    /// The auctioneer needs this mid-sale — a late withdrawal, a duplicate entry — so it
    /// undoes whatever the pool already recorded rather than refusing: a player on the block
    /// is taken off it, and a player already sold is refunded to the team that bought them.
    /// </summary>
    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var player = await _uow.Players.GetByIdAsync(id, ct)
            ?? throw new NotFoundException($"Player {id} was not found.");

        // Absolute, worked out once: a transient-fault retry re-runs the block below, and a
        // relative credit would pay the team twice.
        Team? buyer = null;
        decimal restoredPurse = 0m;
        if (player.Status == PlayerStatus.Sold && player.SoldToTeamId is int teamId)
        {
            buyer = await _uow.Teams.GetByIdAsync(teamId, ct);
            if (buyer is not null)
            {
                restoredPurse = buyer.Purse + (player.SoldPrice ?? 0m);
            }
        }

        var auction = player.AuctionId is int auctionId
            ? await _uow.Auctions.GetByIdAsync(auctionId, ct)
            : null;
        var wasOnTheBlock = auction is not null && auction.CurrentPlayerId == player.Id;

        await _uow.ExecuteInTransactionAsync(async token =>
        {
            if (buyer is not null)
            {
                buyer.Purse = restoredPurse;
            }

            if (wasOnTheBlock && auction is not null)
            {
                auction.CurrentPlayerId = null;
            }

            await _uow.SaveChangesAsync(token);

            // Bids point at the player with no cascade, so they go first.
            await _uow.Bids.DeleteForPlayerAsync(player.Id, token);

            _uow.Players.Remove(player);
            await _uow.SaveChangesAsync(token);
        },
        ct);

        if (player.PhotoKey is { Length: > 0 } key)
        {
            // After the row is gone: a failed delete must not leave a player pointing at a
            // file that no longer exists.
            _photos.Delete(key);
        }

        await BroadcastIfRunningAsync(auction, ct);
    }

    /// <summary>Pushes the room state after a pool change, so open screens do not go stale.</summary>
    private async Task BroadcastIfRunningAsync(Auction? auction, CancellationToken ct)
    {
        if (auction is null || auction.Status is not (AuctionStatus.Live or AuctionStatus.Paused))
        {
            return;
        }

        var state = await _auctions.GetStateAsync(auction.Id, ct);
        if (state is not null)
        {
            await _notifier.StateChangedAsync(state, ct);
        }
    }

    /// <summary>Blank values are dropped so an untouched form field does not become an empty skill.</summary>
    private static string? SerialiseSkills(Dictionary<string, string>? skills)
    {
        if (skills is null) return null;

        var filled = skills
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Value))
            .ToDictionary(pair => pair.Key, pair => pair.Value.Trim());

        return filled.Count == 0 ? null : JsonSerializer.Serialize(filled);
    }
}
