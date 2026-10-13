namespace AuctionApp.Api.Domain.Entities;

/// <summary>
/// A single bid placed on a player. The highest one standing wins.
///
/// Rows are never deleted. An auctioneer who taps the wrong team retracts the bid rather
/// than erasing it, so the record of what happened in the room survives the correction.
/// </summary>
public class Bid
{
    public int Id { get; set; }

    public int AuctionId { get; set; }

    public Auction? Auction { get; set; }

    public int PlayerId { get; set; }

    public Player? Player { get; set; }

    /// <summary>Team the bid counts for, and whose purse it is measured against.</summary>
    public int TeamId { get; set; }

    public Team? Team { get; set; }

    public decimal Amount { get; set; }

    public DateTime PlacedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// True when an admin entered the bid on the team's behalf, which is how bids from a
    /// team that is offline or off-network get into the room.
    /// </summary>
    public bool IsManualOverride { get; set; }

    /// <summary>User who submitted the bid — the owner, or the admin for an offline entry.</summary>
    public int? PlacedByUserId { get; set; }

    public User? PlacedByUser { get; set; }

    /// <summary>
    /// Client-supplied de-duplication key. An owner whose connection drops mid-bid retries
    /// with the same key and gets the original bid back rather than a second one.
    /// </summary>
    public string? ClientRequestId { get; set; }

    /// <summary>
    /// Set when the auctioneer took this bid back. A retracted bid stops counting towards
    /// the standing price and drops out of the feed, but the row stays so the correction
    /// is recoverable and auditable.
    /// </summary>
    public DateTime? RetractedAt { get; set; }

    /// <summary>Admin who retracted it. Null while the bid still stands.</summary>
    public int? RetractedByUserId { get; set; }

    public User? RetractedByUser { get; set; }

    public bool IsRetracted => RetractedAt is not null;
}
