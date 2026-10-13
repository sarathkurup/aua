namespace AuctionApp.Api.Domain.Entities;

/// <summary>
/// A franchise inside one tournament, holding its own purse.
///
/// The purse lives here rather than on the user precisely so that tournaments can run in
/// parallel: an owner bidding in two auctions at once spends two separate budgets.
/// </summary>
public class Team
{
    public int Id { get; set; }

    public int TournamentId { get; set; }

    public Tournament? Tournament { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Short tag shown where the full name will not fit, e.g. "CT".</summary>
    public string? ShortName { get; set; }

    /// <summary>Account that bids for this team. Null when the auctioneer bids for them offline.</summary>
    public int? OwnerUserId { get; set; }

    public User? Owner { get; set; }

    /// <summary>Budget remaining. Debited when a player is sold to this team.</summary>
    public decimal Purse { get; set; }

    /// <summary>Budget the purse is reset to when the auction restarts from draft.</summary>
    public decimal InitialPurse { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Player> Players { get; set; } = new List<Player>();

    public ICollection<Bid> Bids { get; set; } = new List<Bid>();
}
