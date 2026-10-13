using System.ComponentModel.DataAnnotations;
using AuctionApp.Api.Domain;

namespace AuctionApp.Api.Contracts;

/// <summary>What someone fills in to ask for an auction of their own.</summary>
public record CreateAccountRequestRequest
{
    /// <summary>
    /// The username they will sign in with. Chosen here rather than at approval so the
    /// person asking already knows half of their own credentials.
    /// </summary>
    [Required]
    [MinLength(3)]
    [MaxLength(64)]
    [RegularExpression(
        "^[a-zA-Z0-9._-]+$",
        ErrorMessage = "Use letters, numbers, dots, dashes or underscores only.")]
    public string Username { get; init; } = string.Empty;

    [Required]
    [MaxLength(128)]
    public string DisplayName { get; init; } = string.Empty;

    [MaxLength(128)]
    public string? AuctionName { get; init; }

    [MaxLength(256)]
    public string? Contact { get; init; }
}

public record AccountRequestDto(
    int Id,
    string Username,
    string DisplayName,
    string? AuctionName,
    string? Contact,
    AccountRequestStatus Status,
    DateTime RequestedAt,
    DateTime? DecidedAt,
    string? DecidedByName,
    string? DecisionNote);

/// <summary>
/// The one and only time the generated password is readable.
///
/// It is returned to the approving admin so they can pass it on by hand, and only the hash
/// is kept. Nothing can show it again — a lost password has to be reset, which mints a new one.
/// </summary>
public record ApprovalResultDto(
    AccountRequestDto Request,
    string Username,
    string GeneratedPassword);

public record DeclineAccountRequestRequest
{
    [MaxLength(512)]
    public string? Reason { get; init; }
}
