namespace AuctionApp.Api.Domain;

/// <summary>
/// A request that was well-formed but broke an auction rule — bidding under the
/// minimum, selling with no bids, starting an auction that has already finished.
/// Surfaces to the client as 409 Conflict with the message intact, because these
/// are messages the auctioneer needs to read out loud.
/// </summary>
public class AuctionRuleException : Exception
{
    public AuctionRuleException(string message)
        : base(message)
    {
    }
}

/// <summary>A referenced entity does not exist. Surfaces as 404.</summary>
public class NotFoundException : Exception
{
    public NotFoundException(string message)
        : base(message)
    {
    }
}

/// <summary>
/// The caller is signed in but does not control this resource — typically someone else's
/// tournament. Surfaces as 403, and the message names the reason so the UI can show it.
/// </summary>
public class ForbiddenException : Exception
{
    public ForbiddenException(string message)
        : base(message)
    {
    }
}
