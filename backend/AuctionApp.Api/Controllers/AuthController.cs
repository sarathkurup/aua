using AuctionApp.Api.Contracts;
using AuctionApp.Api.Domain;
using AuctionApp.Api.Infrastructure;
using AuctionApp.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuctionApp.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;

    public AuthController(IAuthService auth) => _auth = auth;

    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken ct)
    {
        var result = await _auth.LoginAsync(request, ct);
        return result is null
            ? Unauthorized(new { message = "Incorrect username or password." })
            : Ok(result);
    }

    /// <summary>
    /// Closed deliberately. Accounts are created by the auction admin approving a request at
    /// <c>POST /api/account-requests</c>, which mints a password to be handed over by hand.
    /// Kept as an explicit refusal rather than a 404 so an older client says something useful.
    /// </summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public IActionResult Register() =>
        throw new AuctionRuleException(
            "Sign-up is not open. Request an auction instead, and the auction admin will send " +
            "you a username and password.");

    /// <summary>Re-reads the caller's profile so a refreshed page picks up purse changes.</summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<UserDto>> Me(CancellationToken ct)
    {
        var user = await _auth.GetProfileAsync(User.GetUserId(), ct);
        return user is null ? Unauthorized() : Ok(user);
    }

    /// <summary>
    /// Edits the caller's own profile. There is no route that takes an account id —
    /// the token decides whose profile is being changed.
    /// </summary>
    [HttpPut("me")]
    [Authorize]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<UserDto>> UpdateMe(UpdateProfileRequest request, CancellationToken ct) =>
        Ok(await _auth.UpdateProfileAsync(User.GetUserId(), request, ct));
}
