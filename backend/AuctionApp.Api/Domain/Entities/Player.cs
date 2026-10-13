namespace AuctionApp.Api.Domain.Entities;

/// <summary>A player in a tournament's auction pool. One row is one lot.</summary>
public class Player
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public int TournamentId { get; set; }

    public Tournament? Tournament { get; set; }

    /// <summary>Primary role, e.g. "Batter", "Singles specialist".</summary>
    public string? Role { get; set; }

    /// <summary>Recorded only because squad composition rules can depend on it.</summary>
    public Gender Gender { get; set; } = Gender.Unspecified;

    public string? Country { get; set; }

    /// <summary>
    /// Sport-specific attributes as a JSON object, e.g. {"Batting":"Right-hand","Bowling":"Off-spin"}
    /// for cricket or {"Grip":"Right-hand","Discipline":"Doubles"} for badminton. Kept schemaless so a
    /// new sport needs no migration; the client renders whatever keys it finds.
    /// </summary>
    public string? SkillsJson { get; set; }

    /// <summary>Opening price. The first bid must meet or exceed this.</summary>
    public decimal BasePrice { get; set; }

    /// <summary>
    /// Base name of an uploaded photo, without extension or size suffix. The API serves
    /// /media/players/{key}-sm.webp and -lg.webp from it. Null when no photo was uploaded.
    /// </summary>
    public string? PhotoKey { get; set; }

    /// <summary>Externally hosted photo, used when no file has been uploaded.</summary>
    public string? ImageUrl { get; set; }

    public PlayerStatus Status { get; set; } = PlayerStatus.Available;

    /// <summary>
    /// Whether this player was bought or came with the team. Owners and marquee players are
    /// created straight onto a squad with <see cref="PlayerStatus.Retained"/> and never enter
    /// the pool.
    /// </summary>
    public TeamRole TeamRole { get; set; } = TeamRole.Auctioned;

    /// <summary>Order the auctioneer calls players in. Lower goes first.</summary>
    public int LotOrder { get; set; }

    /// <summary>
    /// Squad number, the one printed on the shirt. Distinct from <see cref="LotOrder"/>,
    /// which is only the calling order for one auction. Null when the league does not use them.
    /// </summary>
    public int? JerseyNumber { get; set; }

    /// <summary>Winning amount, set when the player is sold.</summary>
    public decimal? SoldPrice { get; set; }

    /// <summary>
    /// When the hammer fell. Gives the sold list a real chronological order — lot order
    /// does not, because a passed player can be re-called later.
    /// </summary>
    public DateTime? SoldAt { get; set; }

    /// <summary>Winning team, set when the player is sold.</summary>
    public int? SoldToTeamId { get; set; }

    public Team? SoldToTeam { get; set; }

    /// <summary>Auction this player was sold or passed in. Null while still in the pool.</summary>
    public int? AuctionId { get; set; }

    public Auction? Auction { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Bid> Bids { get; set; } = new List<Bid>();
}
