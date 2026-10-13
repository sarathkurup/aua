using AuctionApp.Api.Contracts;
using AuctionApp.Api.Data.Repositories;
using AuctionApp.Api.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuctionApp.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IUnitOfWork _uow;

    public UsersController(IUnitOfWork uow) => _uow = uow;

    /// <summary>
    /// Accounts that can be handed a team.
    ///
    /// Open to any signed-in account, because anyone may now run a tournament and needs to
    /// assign owners to its teams. It returns names only — never contact details — so that
    /// widening does not turn into a directory of everyone's email and phone number.
    /// Spectators, who have no account, cannot reach it at all.
    /// </summary>
    [HttpGet("owners")]
    public async Task<ActionResult<IReadOnlyList<OwnerOptionDto>>> Owners(CancellationToken ct)
    {
        var owners = await _uow.Users.ListByRoleAsync(UserRole.TeamOwner, ct);
        return Ok(owners.Select(o => new OwnerOptionDto(o.Id, o.DisplayName)).ToList());
    }
}
