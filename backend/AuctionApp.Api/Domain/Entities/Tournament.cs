namespace AuctionApp.Api.Domain.Entities;

/// <summary>
/// A competition: one sport, one player pool, one set of teams, and the squad rules every
/// team in it must satisfy. Auctions belong to a tournament, and several tournaments can be
/// auctioning at the same moment without touching each other's purses or pools.
/// </summary>
public class Tournament
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public int SportId { get; set; }

    public Sport? Sport { get; set; }

    public TournamentType Type { get; set; } = TournamentType.Professional;

    /// <summary>
    /// The account that created this tournament and acts as its auctioneer. Whoever owns it
    /// may manage its rules, pool, teams and auctions; a site admin may manage any of them.
    /// Null only for tournaments that predate ownership.
    /// </summary>
    public int? OwnerUserId { get; set; }

    public User? Owner { get; set; }

    public string? Description { get; set; }

    /// <summary>Purse each team starts with, and is reset to when an auction restarts from draft.</summary>
    public decimal TeamPurse { get; set; } = 100_000_000m;

    // ------------------------------------------------------------------ squad rules

    /// <summary>Players a team must end up with. Zero disables the requirement.</summary>
    public int MinSquadSize { get; set; }

    /// <summary>Players a team may not exceed. Zero disables the cap.</summary>
    public int MaxSquadSize { get; set; }

    /// <summary>
    /// Women a team must end up with — the workplace-league rule. Zero disables it.
    /// Counted against <see cref="Player.Gender"/>.
    /// </summary>
    public int MinWomenPerSquad { get; set; }

    /// <summary>Ceiling on a single winning bid, so one signing cannot swallow a purse. Null disables it.</summary>
    public decimal? MaxSpendPerPlayer { get; set; }

    /// <summary>
    /// When set, a bid is refused if winning it would leave the team unable to fill its
    /// remaining mandatory slots at the cheapest base price still in the pool. This is what
    /// stops a team from spending its way out of compliance early in the auction.
    /// </summary>
    public bool ReservePurseForUnfilledSlots { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Team> Teams { get; set; } = new List<Team>();

    public ICollection<Player> Players { get; set; } = new List<Player>();

    public ICollection<Auction> Auctions { get; set; } = new List<Auction>();
}
