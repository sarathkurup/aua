using System.ComponentModel.DataAnnotations;
using AuctionApp.Api.Domain;

namespace AuctionApp.Api.Contracts;

public record SportDto(int Id, string Name, string? Description, string? Icon, IReadOnlyList<string> SkillFields);

/// <summary>The squad constraints every team in a tournament must satisfy.</summary>
public record SquadRulesDto(
    int MinSquadSize,
    int MaxSquadSize,
    int MinWomenPerSquad,
    decimal? MaxSpendPerPlayer,
    bool ReservePurseForUnfilledSlots);

public record TournamentDto(
    int Id,
    string Name,
    string? Description,
    int SportId,
    string SportName,
    string? SportIcon,
    TournamentType Type,
    decimal TeamPurse,
    SquadRulesDto Rules,
    int TeamCount,
    int PlayerCount,
    /// <summary>Display name of the auctioneer who created it. Null for legacy rows.</summary>
    string? OwnerName,
    /// <summary>True when the caller may change this tournament — the owner, or an admin.</summary>
    bool CanManage);

public record SaveTournamentRequest
{
    [Required]
    [MaxLength(128)]
    public string Name { get; init; } = string.Empty;

    [MaxLength(512)]
    public string? Description { get; init; }

    [Range(1, int.MaxValue, ErrorMessage = "A sport must be selected.")]
    public int SportId { get; init; }

    public TournamentType Type { get; init; } = TournamentType.Professional;

    [Range(1, 100_000_000_000d, ErrorMessage = "Team purse must be greater than zero.")]
    public decimal TeamPurse { get; init; } = 100_000_000m;

    [Range(0, 100)]
    public int MinSquadSize { get; init; }

    [Range(0, 100)]
    public int MaxSquadSize { get; init; }

    [Range(0, 100)]
    public int MinWomenPerSquad { get; init; }

    [Range(0, 100_000_000_000d)]
    public decimal? MaxSpendPerPlayer { get; init; }

    public bool ReservePurseForUnfilledSlots { get; init; } = true;
}

/// <summary>
/// A permanent squad member: the team's owner, or a marquee signing. These never enter the
/// pool, so there is no base price or lot order to give them.
/// </summary>
public record SaveTeamMemberRequest
{
    [Required]
    [MaxLength(128)]
    public string Name { get; init; } = string.Empty;

    /// <summary>Owner or Marquee. Auctioned is rejected — those come from the pool.</summary>
    public TeamRole TeamRole { get; init; } = TeamRole.Marquee;

    [MaxLength(64)]
    public string? Role { get; init; }

    public Gender Gender { get; init; } = Gender.Unspecified;

    [MaxLength(64)]
    public string? Country { get; init; }

    public Dictionary<string, string>? Skills { get; init; }
}

public record SaveTeamRequest
{
    [Required]
    [MaxLength(128)]
    public string Name { get; init; } = string.Empty;

    [MaxLength(8)]
    public string? ShortName { get; init; }

    /// <summary>Account that bids for this team. Null leaves the team offline-only.</summary>
    public int? OwnerUserId { get; init; }
}
