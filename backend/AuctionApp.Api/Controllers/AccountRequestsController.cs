using AuctionApp.Api.Contracts;
using AuctionApp.Api.Domain;
using AuctionApp.Api.Infrastructure;
using AuctionApp.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuctionApp.Api.Controllers;

/// <summary>
/// Requests to run an auction, and the auction admin's decisions on them.
///
/// This is the only way into the app: there is no self-service sign-up, because credentials
/// are handed over in person or over Teams rather than emailed.
/// </summary>
[ApiController]
[Route("api/account-requests")]
public class AccountRequestsController : ControllerBase
{
    private readonly IAccountRequestService _requests;

    public AccountRequestsController(IAccountRequestService requests) => _requests = requests;

    /// <summary>Open to anyone on the network — asking costs nothing and grants nothing.</summary>
    [HttpPost]
    [AllowAnonymous]
    public async Task<ActionResult<AccountRequestDto>> Submit(
        CreateAccountRequestRequest request,
        CancellationToken ct) =>
        Ok(await _requests.SubmitAsync(request, ct));

    [HttpGet]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<ActionResult<IReadOnlyList<AccountRequestDto>>> List(
        [FromQuery] AccountRequestStatus? status,
        CancellationToken ct) =>
        Ok(await _requests.ListAsync(status, ct));

    /// <summary>
    /// Approves a request and returns the generated password. The response is the only place
    /// it will ever appear, so the console shows it until the admin dismisses it.
    /// </summary>
    [HttpPost("{id:int}/approve")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<ActionResult<ApprovalResultDto>> Approve(int id, CancellationToken ct) =>
        Ok(await _requests.ApproveAsync(id, User.GetUserId(), ct));

    [HttpPost("{id:int}/decline")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<ActionResult<AccountRequestDto>> Decline(
        int id,
        DeclineAccountRequestRequest request,
        CancellationToken ct) =>
        Ok(await _requests.DeclineAsync(id, User.GetUserId(), request.Reason, ct));

    /// <summary>Issues a new password for an existing account whose old one was lost.</summary>
    [HttpPost("users/{userId:int}/reset-password")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<ActionResult<ApprovalResultDto>> ResetPassword(int userId, CancellationToken ct) =>
        Ok(await _requests.ResetPasswordAsync(userId, ct));
}
