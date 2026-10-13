namespace AuctionApp.Api.Domain.Entities;

/// <summary>
/// Someone asking to run an auction.
///
/// There is no email or one-time code in this flow by design: the whole thing happens inside
/// one office. A requester picks the username they want, the auction admin approves it, and
/// the API mints a random password that is shown to the admin exactly once so they can pass
/// it on by hand. Nothing is ever emailed, and the password is only ever stored hashed.
/// </summary>
public class AccountRequest
{
    public int Id { get; set; }

    /// <summary>The username they will sign in with. Checked for collisions at approval.</summary>
    public string Username { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    /// <summary>What they want to run — shown to the admin deciding on the request.</summary>
    public string? AuctionName { get; set; }

    /// <summary>How to reach them to hand over the password. Free text: a Teams handle, a desk, a phone.</summary>
    public string? Contact { get; set; }

    public AccountRequestStatus Status { get; set; } = AccountRequestStatus.Pending;

    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;

    public DateTime? DecidedAt { get; set; }

    /// <summary>Admin who approved or declined it.</summary>
    public int? DecidedByUserId { get; set; }

    public User? DecidedByUser { get; set; }

    /// <summary>Account created on approval. Null until then, and for declined requests.</summary>
    public int? CreatedUserId { get; set; }

    public User? CreatedUser { get; set; }

    /// <summary>Why it was declined, so the admin can say something useful when they reply.</summary>
    public string? DecisionNote { get; set; }
}
