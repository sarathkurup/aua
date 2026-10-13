using System.ComponentModel.DataAnnotations;
using AuctionApp.Api.Domain;

namespace AuctionApp.Api.Contracts;

public record LoginRequest
{
    [Required]
    [MaxLength(64)]
    public string Username { get; init; } = string.Empty;

    [Required]
    [MaxLength(128)]
    public string Password { get; init; } = string.Empty;
}

/// <summary>A team the signed-in owner bids for, with the tournament it belongs to.</summary>
public record OwnedTeamDto(
    int TeamId,
    string TeamName,
    string? ShortName,
    int TournamentId,
    string TournamentName,
    string SportName,
    decimal Purse);

/// <summary>
/// The signed-in account's own profile. Returned only to the account it describes, which is
/// why the contact fields are safe to include; <c>PublicId</c> is the identifier anything
/// user-facing should key on, never <c>Id</c>.
/// </summary>
public record UserDto(
    int Id,
    Guid PublicId,
    string Username,
    string DisplayName,
    UserRole Role,
    string? Email,
    string? PhoneNumber,
    string? AvatarUrl,
    IReadOnlyList<OwnedTeamDto> Teams);

/// <summary>
/// An account that can be handed a team, for the auctioneer's owner picker.
///
/// Deliberately just enough to fill a dropdown. <see cref="UserDto"/> carries contact
/// details and is only ever returned to the account it describes; handing that shape to
/// every auctioneer would publish everyone's email and phone number.
/// </summary>
public record OwnerOptionDto(int Id, string DisplayName);

/// <summary>Fields an account may change about itself.</summary>
public record UpdateProfileRequest
{
    [Required]
    [MaxLength(128)]
    public string DisplayName { get; init; } = string.Empty;

    [EmailAddress]
    [MaxLength(256)]
    public string? Email { get; init; }

    /// <summary>Stored for the mobile-OTP sign-in that will replace passwords later.</summary>
    [Phone]
    [MaxLength(32)]
    public string? PhoneNumber { get; init; }

    [MaxLength(512)]
    public string? AvatarUrl { get; init; }
}

/// <summary>Self-service sign-up. New accounts are team owners; admin is granted, never claimed.</summary>
public record RegisterRequest
{
    [Required]
    [MaxLength(64)]
    [RegularExpression("^[a-zA-Z0-9._-]+$", ErrorMessage = "Use letters, numbers, dots, dashes or underscores.")]
    public string Username { get; init; } = string.Empty;

    [Required]
    [MaxLength(128)]
    public string DisplayName { get; init; } = string.Empty;

    [Required]
    [MinLength(8, ErrorMessage = "Use at least 8 characters.")]
    [MaxLength(128)]
    public string Password { get; init; } = string.Empty;

    [EmailAddress]
    [MaxLength(256)]
    public string? Email { get; init; }

    [Phone]
    [MaxLength(32)]
    public string? PhoneNumber { get; init; }
}

public record AuthResponse(string Token, DateTime ExpiresAtUtc, UserDto User);
