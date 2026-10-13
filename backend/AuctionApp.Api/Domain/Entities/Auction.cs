namespace AuctionApp.Api.Domain.Entities;

/// <summary>
/// An auction event over a tournament's player pool. A tournament may hold more than one
/// (a marquee round, then the main round), and auctions in different tournaments run
/// side by side.
/// </summary>
public class Auction
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public int TournamentId { get; set; }

    public Tournament? Tournament { get; set; }

    public AuctionStatus Status { get; set; } = AuctionStatus.Draft;

    /// <summary>Whether the room is advertised on the public lobby. See <see cref="AuctionVisibility"/>.</summary>
    public AuctionVisibility Visibility { get; set; } = AuctionVisibility.Public;

    /// <summary>
    /// The room's shareable identity, and the only address spectators are ever given.
    /// Unguessable, so it doubles as the capability that lets link holders — and nobody
    /// else — watch a private auction. Sequential <see cref="Id"/> values stay server-side
    /// precisely because they can be enumerated.
    /// </summary>
    public Guid PublicId { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Smallest raise over the standing bid. Used as the flat step when no ladder is
    /// configured, and as the fallback if a stored ladder cannot be read.
    /// </summary>
    public decimal MinBidIncrement { get; set; } = 100_000m;

    /// <summary>
    /// Floor for the opening bid, whatever a player's base price says. Zero means the base
    /// price alone decides.
    /// </summary>
    public decimal MinimumBid { get; set; }

    /// <summary>
    /// Increment ladder as JSON, e.g. <c>[{"From":0,"Increment":1000},{"From":10000,"Increment":5000}]</c>.
    /// Null falls back to a flat <see cref="MinBidIncrement"/>. See <see cref="BidLadder"/>.
    /// </summary>
    public string? IncrementRulesJson { get; set; }

    /// <summary>
    /// Seconds a lot stays open with no new bid before it resolves itself. Zero leaves every
    /// lot open until the auctioneer calls it.
    /// </summary>
    public int AutoTimeoutSeconds { get; set; }

    /// <summary>
    /// When the lot on the block runs out. Held on the server, not in the auctioneer's tab,
    /// so a closed browser cannot leave a lot open forever — and so every screen counts down
    /// to the same instant.
    /// </summary>
    public DateTime? LotDeadlineAt { get; set; }

    /// <summary>Player currently on the block. Null between lots.</summary>
    public int? CurrentPlayerId { get; set; }

    public Player? CurrentPlayer { get; set; }

    /// <summary>
    /// When the auctioneer intends to open the room, at most 30 days out. Null means it can
    /// be started whenever. A scheduled auction cannot be started early — clear or move the
    /// schedule to open it sooner.
    /// </summary>
    public DateTime? ScheduledStartAt { get; set; }

    public DateTime? StartedAt { get; set; }

    public DateTime? EndedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Bid> Bids { get; set; } = new List<Bid>();

    /// <summary>Players resolved (sold or unsold) in this auction.</summary>
    public ICollection<Player> Players { get; set; } = new List<Player>();
}
