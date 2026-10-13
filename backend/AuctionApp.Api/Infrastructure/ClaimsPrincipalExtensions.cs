using System.Security.Claims;
using AuctionApp.Api.Domain;

namespace AuctionApp.Api.Infrastructure;

public static class ClaimsPrincipalExtensions
{
    /// <summary>Id of the authenticated user. Throws if the token has no usable subject.</summary>
    public static int GetUserId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(value, out var id)
            ? id
            : throw new UnauthorizedAccessException("The access token does not identify a user.");
    }

    /// <summary>Non-throwing form, for paths where an anonymous caller is legitimate.</summary>
    public static bool TryGetUserId(this ClaimsPrincipal principal, out int userId) =>
        int.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out userId);

    public static bool IsAdmin(this ClaimsPrincipal principal) =>
        principal.IsInRole(nameof(UserRole.Admin));
}
