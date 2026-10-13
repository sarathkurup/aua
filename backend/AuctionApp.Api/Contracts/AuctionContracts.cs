using System.ComponentModel.DataAnnotations;
using AuctionApp.Api.Domain;

namespace AuctionApp.Api.Contracts;

public record AuctionDto(
    int Id,
    /// <summary>The room's share address. Clients route on this, never on <c>Id</c>.</summary>
    Guid PublicId,
    string Name,
    int TournamentId,
    string TournamentName,
    int SportId,
    string SportName,
    string? SportIcon,
    TournamentType TournamentType,
    AuctionStatus Status,
    AuctionVisibility Visibility,
    decimal MinBidIncrement,
    decimal MinimumBid,
    /// <summary>The increment ladder, lowest rung first.</summary>
    IReadOnlyList<BidIncrementRule> Increments,
    /// <summary>Seconds a lot stays open with no bid. Zero disables the timer.</summary>
    int AutoTimeoutSeconds,
    /// <summary>When the current lot expires. Null when nothing is on the block or the timer is off.</summary>
    DateTime? LotDeadlineAt,
    int? CurrentPlayerId,
    DateTime? ScheduledStartAt,
    DateTime? StartedAt,
    DateTime? EndedAt);

/// <summary>
/// Everything a client needs to render the room in one payload. Broadcast over SignalR
/// after every state change so late joiners and reconnecting clients converge.
/// </summary>
public record AuctionStateDto(
    AuctionDto Auction,
    SquadRulesDto Rules,
    PlayerDto? CurrentPlayer,
    BidDto? TopBid,
    decimal NextMinimumBid,
    IReadOnlyList<BidDto> RecentBids,
    IReadOnlyList<TeamSummaryDto> Teams,
    /// <summary>The sale that reopening would undo. Null before anything has sold.</summary>
    LastSaleDto? LastSale);

/// <summary>
/// The most recent completed sale, so the console can name what "reopen" would put back
/// rather than offering an unlabelled button.
/// </summary>
public record LastSaleDto(
    int PlayerId,
    string PlayerName,
    int TeamId,
    string TeamName,
    decimal Amount,
    DateTime SoldAt);

/// <summary>
/// A team's standing, including how far it is from satisfying the tournament's squad
/// rules — which is what the auctioneer watches as a corporate auction runs down.
/// </summary>
public record TeamSummaryDto(
    int TeamId,
    string Name,
    string? ShortName,
    int? OwnerUserId,
    decimal Purse,
    decimal InitialPurse,
    int PlayersWon,
    int WomenWon,
    decimal TotalSpent,
    /// <summary>Signings still needed to reach the minimum squad size.</summary>
    int SlotsRemaining,
    /// <summary>Women still needed to satisfy the participation rule.</summary>
    int WomenRequired,
    /// <summary>Purse the team must keep back to fill its remaining mandatory slots.</summary>
    decimal ReservedPurse,
    /// <summary>Purse the team may actually commit to the current lot.</summary>
    decimal SpendablePurse,
    /// <summary>False once the team has bought its maximum squad size.</summary>
    bool CanBid,
    /// <summary>Set when the team cannot bid, explaining why.</summary>
    string? BlockedReason,
    /// <summary>Bought players plus retained ones — what the squad rules are measured against.</summary>
    int SquadSize,
    /// <summary>The owner and marquee players the team started with, owner first.</summary>
    IReadOnlyList<TeamMemberDto> Retained);

/// <summary>
/// A permanent squad member. Carries just enough to identify a face in the team list —
/// the full profile is still a player record if anyone needs it.
/// </summary>
public record TeamMemberDto(
    int PlayerId,
    string Name,
    TeamRole TeamRole,
    string? Role,
    Gender Gender,
    string? PhotoThumbUrl,
    string? PhotoUrl);

/// <summary>
/// One card on the dashboard — a room that is running now, or one that is scheduled.
/// The live-only fields are null for a scheduled card, which has no lot or bids yet.
/// </summary>
public record LiveAuctionCardDto(
    Guid PublicId,
    AuctionVisibility Visibility,
    DateTime? ScheduledStartAt,
    string AuctionName,
    string TournamentName,
    string SportName,
    string? SportIcon,
    TournamentType TournamentType,
    AuctionStatus Status,
    string? CurrentPlayerName,
    string? CurrentPlayerPhotoThumbUrl,
    decimal? CurrentBid,
    string? CurrentBidderTeam,
    int TeamCount,
    int PlayersSold,
    int PlayersTotal);

public record CreateAuctionRequest
{
    [Required]
    [MaxLength(128)]
    public string Name { get; init; } = string.Empty;

    [Range(1, int.MaxValue, ErrorMessage = "A tournament must be selected.")]
    public int TournamentId { get; init; }

    [Range(1, 1_000_000_000d, ErrorMessage = "Minimum increment must be greater than zero.")]
    public decimal MinBidIncrement { get; init; } = 100_000m;

    /// <summary>
    /// Public lists the room on the lobby, which exposes the player photos and profiles to
    /// anyone. Private keeps it unlisted and reachable only through its share link.
    /// </summary>
    public AuctionVisibility Visibility { get; init; } = AuctionVisibility.Public;

    /// <summary>Optional opening time, at most <see cref="AuctionSchedule.MaxDaysAhead"/> days out.</summary>
    public DateTime? ScheduledStartAt { get; init; }

    /// <summary>Floor for the opening bid, whatever the player's base price is. Zero disables it.</summary>
    [Range(0, 1_000_000_000d)]
    public decimal MinimumBid { get; init; }

    /// <summary>
    /// Increment ladder. Each rung raises the step once the bid reaches its threshold —
    /// ₹1,000 from the start, ₹2,000 past ₹10,000, ₹5,000 past ₹20,000. Empty falls back to a
    /// flat <see cref="MinBidIncrement"/>.
    /// </summary>
    public IReadOnlyList<BidIncrementRule>? Increments { get; init; }

    /// <summary>Seconds a lot stays open with no bid before it resolves. Zero disables the timer.</summary>
    [Range(0, 600, ErrorMessage = "A lot timer must be between 0 and 600 seconds.")]
    public int AutoTimeoutSeconds { get; init; }
}

/// <summary>Edits to an existing auction. The auctioneer may make these at any time.</summary>
public record SaveAuctionRequest
{
    [Required]
    [MaxLength(128)]
    public string Name { get; init; } = string.Empty;

    [Range(1, 1_000_000_000d, ErrorMessage = "Minimum increment must be greater than zero.")]
    public decimal MinBidIncrement { get; init; } = 100_000m;

    public AuctionVisibility Visibility { get; init; } = AuctionVisibility.Public;

    public DateTime? ScheduledStartAt { get; init; }

    [Range(0, 1_000_000_000d)]
    public decimal MinimumBid { get; init; }

    public IReadOnlyList<BidIncrementRule>? Increments { get; init; }

    [Range(0, 600, ErrorMessage = "A lot timer must be between 0 and 600 seconds.")]
    public int AutoTimeoutSeconds { get; init; }
}

public static class AuctionSchedule
{
    /// <summary>How far ahead an auction may be scheduled.</summary>
    public const int MaxDaysAhead = 30;
}
