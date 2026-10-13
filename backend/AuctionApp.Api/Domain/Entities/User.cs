namespace AuctionApp.Api.Domain.Entities;

/// <summary>
/// An application account. Admins run auctions; team owners bid for the teams they own.
/// Spectators are not users at all — the viewer screens need no account.
///
/// Purses live on <see cref="Team"/>, not here, so one owner can bid in two tournaments
/// at once without the budgets bleeding into each other.
/// </summary>
public class User
{
    public int Id { get; set; }

    /// <summary>
    /// The account's external identity. Anything that leaves the server — a URL, a share
    /// link, a payload another person can see — uses this, never <see cref="Id"/>, so a
    /// profile can never be reached by counting upwards from someone else's.
    /// </summary>
    public Guid PublicId { get; set; } = Guid.NewGuid();

    public string Username { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public UserRole Role { get; set; } = UserRole.TeamOwner;

    // ------------------------------------------------------------------ profile

    public string? Email { get; set; }

    /// <summary>
    /// E.164 mobile number. Optional today and only shown back to its owner; it is stored
    /// now so switching sign-in to mobile OTP later does not need a data migration.
    /// </summary>
    public string? PhoneNumber { get; set; }

    public string? AvatarUrl { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Teams this account bids for, across every tournament.</summary>
    public ICollection<Team> Teams { get; set; } = new List<Team>();

    /// <summary>Tournaments this account created and runs the auctions for.</summary>
    public ICollection<Tournament> TournamentsOwned { get; set; } = new List<Tournament>();
}
