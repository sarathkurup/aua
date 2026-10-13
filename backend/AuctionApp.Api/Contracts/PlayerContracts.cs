using System.ComponentModel.DataAnnotations;
using AuctionApp.Api.Domain;

namespace AuctionApp.Api.Contracts;

public record PlayerDto(
    int Id,
    string Name,
    int TournamentId,
    string SportName,
    string? Role,
    Gender Gender,
    string? Country,
    IReadOnlyDictionary<string, string> Skills,
    decimal BasePrice,
    /// <summary>Small square for tables and the bid feed. Immutable URL, safe to cache forever.</summary>
    string? PhotoThumbUrl,
    /// <summary>Larger crop for the on-block card.</summary>
    string? PhotoUrl,
    PlayerStatus Status,
    /// <summary>Bought at auction, or retained as the team's owner or a marquee signing.</summary>
    TeamRole TeamRole,
    int LotOrder,
    /// <summary>Shirt number, shown as #N. Falls back to lot order in the UI when absent.</summary>
    int? JerseyNumber,
    decimal? SoldPrice,
    /// <summary>When the hammer fell, so the sold list can be ordered by sale rather than lot.</summary>
    DateTime? SoldAt,
    int? SoldToTeamId,
    string? SoldToTeamName,
    decimal? CurrentBid,
    int? CurrentBidderTeamId,
    string? CurrentBidderTeam);

public record SavePlayerRequest
{
    [Required]
    [MaxLength(128)]
    public string Name { get; init; } = string.Empty;

    [Range(1, int.MaxValue, ErrorMessage = "A tournament must be selected.")]
    public int TournamentId { get; init; }

    [MaxLength(64)]
    public string? Role { get; init; }

    public Gender Gender { get; init; } = Gender.Unspecified;

    [MaxLength(64)]
    public string? Country { get; init; }

    /// <summary>Sport-specific attributes, keyed by the sport's declared skill fields.</summary>
    public Dictionary<string, string>? Skills { get; init; }

    [Range(0.01, 1_000_000_000d, ErrorMessage = "Base price must be greater than zero.")]
    public decimal BasePrice { get; init; }

    [MaxLength(512)]
    public string? ImageUrl { get; init; }

    [Range(0, int.MaxValue)]
    public int LotOrder { get; init; }

    [Range(0, 999, ErrorMessage = "A jersey number must be between 0 and 999.")]
    public int? JerseyNumber { get; init; }
}
