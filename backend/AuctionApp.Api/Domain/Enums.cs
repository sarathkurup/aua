namespace AuctionApp.Api.Domain;

/// <summary>Access level for an application user. Spectators need no account at all.</summary>
public enum UserRole
{
    /// <summary>Bids on behalf of the teams they own.</summary>
    TeamOwner = 0,

    /// <summary>Runs auctions: manages tournaments, players, the lifecycle, and offline bids.</summary>
    Admin = 1,
}

/// <summary>
/// Which rule book a tournament runs under. The type is what the admin picks first;
/// it seeds sensible defaults for the squad rules, which stay editable afterwards.
/// </summary>
public enum TournamentType
{
    /// <summary>Open competition. No composition requirement beyond squad size.</summary>
    Professional = 0,

    /// <summary>
    /// Workplace league. These normally carry participation requirements — most often a
    /// minimum number of women in every squad.
    /// </summary>
    Corporate = 1,
}

/// <summary>
/// Recorded because squad composition rules depend on it. Unspecified is allowed so a
/// tournament that does not use gender rules never has to fill it in.
/// </summary>
public enum Gender
{
    Unspecified = 0,
    Male = 1,
    Female = 2,
}

/// <summary>
/// Who can reach an auction room. The share link works for both; visibility only decides
/// whether the room is also advertised on the public lobby.
/// </summary>
public enum AuctionVisibility
{
    /// <summary>
    /// Listed on the lobby for anyone to open, which puts the player photos and profiles
    /// in front of the public.
    /// </summary>
    Public = 0,

    /// <summary>
    /// Unlisted. Reachable only by its share link, so nothing about the room — including
    /// player photos — is visible to anyone who was not given the link.
    /// </summary>
    Private = 1,
}

/// <summary>Lifecycle of an auction event.</summary>
public enum AuctionStatus
{
    /// <summary>Created but not yet opened to viewers.</summary>
    Draft = 0,

    /// <summary>Accepting bids.</summary>
    Live = 1,

    /// <summary>Temporarily halted; bids are rejected but state is preserved.</summary>
    Paused = 2,

    /// <summary>Finished. Results are read-only.</summary>
    Completed = 3,
}

/// <summary>Where a player sits in the auction pool.</summary>
public enum PlayerStatus
{
    /// <summary>In the pool, not yet called.</summary>
    Available = 0,

    /// <summary>Currently being auctioned; bids are accepted for this player only.</summary>
    OnBlock = 1,

    /// <summary>Bought by a team.</summary>
    Sold = 2,

    /// <summary>Called but attracted no bid.</summary>
    Unsold = 3,

    /// <summary>
    /// Already on a team and never auctioned — the owner and the marquee players a squad
    /// starts with. They count as squad members but are not part of the pool, so restarting
    /// an auction leaves them where they are.
    /// </summary>
    Retained = 4,
}

/// <summary>Where a request to run an auction has got to.</summary>
public enum AccountRequestStatus
{
    /// <summary>Waiting for the auction admin to decide.</summary>
    Pending = 0,

    /// <summary>Approved; an account exists and its password has been issued once.</summary>
    Approved = 1,

    /// <summary>Turned down. No account was created.</summary>
    Declined = 2,
}

/// <summary>How a player came to be on their team.</summary>
public enum TeamRole
{
    /// <summary>Bought at auction. The default for everyone in the pool.</summary>
    Auctioned = 0,

    /// <summary>The team's owner, who also plays for it.</summary>
    Owner = 1,

    /// <summary>
    /// A marquee signing the team starts with — usually an established player, handed to
    /// the squad rather than bid for.
    /// </summary>
    Marquee = 2,
}
