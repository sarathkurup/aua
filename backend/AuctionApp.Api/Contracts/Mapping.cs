using System.Text.Json;
using AuctionApp.Api.Domain;
using AuctionApp.Api.Domain.Entities;

namespace AuctionApp.Api.Contracts;

/// <summary>Entity to DTO projections. Kept in one place so shapes stay consistent
/// between REST responses and SignalR broadcasts.</summary>
public static class Mapping
{
    /// <summary>Where uploaded player photos are served from. Matches PhotoService's output path.</summary>
    public const string MediaRoot = "/media/players";

    private static readonly IReadOnlyList<string> NoFields = [];

    private static readonly IReadOnlyDictionary<string, string> NoSkills =
        new Dictionary<string, string>();

    public static UserDto ToDto(this User user, IEnumerable<Team>? teams = null) =>
        new(
            user.Id,
            user.PublicId,
            user.Username,
            user.DisplayName,
            user.Role,
            user.Email,
            user.PhoneNumber,
            user.AvatarUrl,
            (teams ?? user.Teams ?? []).Select(t => t.ToOwnedDto()).ToList());

    public static OwnedTeamDto ToOwnedDto(this Team team) =>
        new(
            team.Id,
            team.Name,
            team.ShortName,
            team.TournamentId,
            team.Tournament?.Name ?? string.Empty,
            team.Tournament?.Sport?.Name ?? string.Empty,
            team.Purse);

    public static SportDto ToDto(this Sport sport) =>
        new(sport.Id, sport.Name, sport.Description, sport.Icon, ParseFields(sport.SkillFieldsJson));

    public static SquadRulesDto ToRulesDto(this Tournament tournament) =>
        new(
            tournament.MinSquadSize,
            tournament.MaxSquadSize,
            tournament.MinWomenPerSquad,
            tournament.MaxSpendPerPlayer,
            tournament.ReservePurseForUnfilledSlots);

    public static TournamentDto ToDto(
        this Tournament tournament,
        int teamCount = 0,
        int playerCount = 0,
        bool canManage = false) =>
        new(
            tournament.Id,
            tournament.Name,
            tournament.Description,
            tournament.SportId,
            tournament.Sport?.Name ?? string.Empty,
            tournament.Sport?.Icon,
            tournament.Type,
            tournament.TeamPurse,
            tournament.ToRulesDto(),
            teamCount == 0 ? tournament.Teams?.Count ?? 0 : teamCount,
            playerCount == 0 ? tournament.Players?.Count ?? 0 : playerCount,
            tournament.Owner?.DisplayName,
            canManage);

    /// <summary>The slimmed-down shape used for retained members on a team card.</summary>
    public static TeamMemberDto ToMemberDto(this Player player) =>
        new(
            player.Id,
            player.Name,
            player.TeamRole,
            player.Role,
            player.Gender,
            PhotoUrl(player, "sm"),
            PhotoUrl(player, "lg"));

    public static PlayerDto ToDto(this Player player, Bid? topBid = null) =>
        new(
            player.Id,
            player.Name,
            player.TournamentId,
            player.Tournament?.Sport?.Name ?? string.Empty,
            player.Role,
            player.Gender,
            player.Country,
            ParseSkills(player.SkillsJson),
            player.BasePrice,
            PhotoUrl(player, "sm"),
            PhotoUrl(player, "lg"),
            player.Status,
            player.TeamRole,
            player.LotOrder,
            player.JerseyNumber,
            player.SoldPrice,
            player.SoldAt,
            player.SoldToTeamId,
            player.SoldToTeam?.Name,
            topBid?.Amount,
            topBid?.TeamId,
            topBid?.Team?.Name);

    public static BidDto ToDto(this Bid bid) =>
        new(
            bid.Id,
            bid.AuctionId,
            bid.PlayerId,
            bid.Player?.Name ?? string.Empty,
            bid.TeamId,
            bid.Team?.Name ?? string.Empty,
            bid.Team?.ShortName,
            bid.Amount,
            bid.PlacedAt,
            bid.IsManualOverride);

    public static AuctionDto ToDto(this Auction auction) =>
        new(
            auction.Id,
            auction.PublicId,
            auction.Name,
            auction.TournamentId,
            auction.Tournament?.Name ?? string.Empty,
            auction.Tournament?.SportId ?? 0,
            auction.Tournament?.Sport?.Name ?? string.Empty,
            auction.Tournament?.Sport?.Icon,
            auction.Tournament?.Type ?? TournamentType.Professional,
            auction.Status,
            auction.Visibility,
            auction.MinBidIncrement,
            auction.MinimumBid,
            BidLadder.Parse(auction.IncrementRulesJson, auction.MinBidIncrement),
            auction.AutoTimeoutSeconds,
            auction.LotDeadlineAt,
            auction.CurrentPlayerId,
            auction.ScheduledStartAt,
            auction.StartedAt,
            auction.EndedAt);

    /// <summary>
    /// An uploaded photo wins over an external URL. The key is content-derived, so the URL
    /// changes whenever the image does and can be cached indefinitely.
    /// </summary>
    private static string? PhotoUrl(Player player, string size) =>
        player.PhotoKey is { Length: > 0 } key ? $"{MediaRoot}/{key}-{size}.webp" : player.ImageUrl;

    private static IReadOnlyList<string> ParseFields(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return NoFields;

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? NoFields;
        }
        catch (JsonException)
        {
            return NoFields;
        }
    }

    private static IReadOnlyDictionary<string, string> ParseSkills(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return NoSkills;

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? NoSkills;
        }
        catch (JsonException)
        {
            return NoSkills;
        }
    }
}
