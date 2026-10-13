using System.ComponentModel.DataAnnotations;

namespace AuctionApp.Api.Contracts;

public record BidDto(
    int Id,
    int AuctionId,
    int PlayerId,
    string PlayerName,
    int TeamId,
    string TeamName,
    string? TeamShortName,
    decimal Amount,
    DateTime PlacedAt,
    bool IsManualOverride);

public record PlaceBidRequest
{
    [Range(1, int.MaxValue)]
    public int PlayerId { get; init; }

    /// <summary>
    /// Team the bid counts for. An owner with one team may leave this unset; it is required
    /// when they own teams in more than one tournament.
    /// </summary>
    public int? TeamId { get; init; }

    [Range(0.01, 1_000_000_000d, ErrorMessage = "Bid amount must be greater than zero.")]
    public decimal Amount { get; init; }

    /// <summary>
    /// Client-generated id used to make retries idempotent. An owner that loses their
    /// connection mid-bid resends the same key, and the server returns the original
    /// bid instead of recording a duplicate.
    /// </summary>
    [MaxLength(64)]
    public string? ClientRequestId { get; init; }
}

/// <summary>An admin entering a bid for a team that cannot reach the app itself.</summary>
public record ManualBidRequest
{
    [Range(1, int.MaxValue)]
    public int PlayerId { get; init; }

    [Range(1, int.MaxValue, ErrorMessage = "A team must be selected.")]
    public int TeamId { get; init; }

    [Range(0.01, 1_000_000_000d, ErrorMessage = "Bid amount must be greater than zero.")]
    public decimal Amount { get; init; }
}
