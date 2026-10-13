using AuctionApp.Api.Contracts;
using AuctionApp.Api.Infrastructure;
using AuctionApp.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuctionApp.Api.Controllers;

/// <summary>
/// Reading a tournament is open to everyone — the lobby and viewer screens need it.
/// Changing one is restricted to the account that created it, or to a site admin; the
/// check runs per row, so it lives in the action rather than on an attribute.
/// </summary>
[ApiController]
[Route("api/tournaments")]
public class TournamentsController : ControllerBase
{
    private readonly ITournamentService _tournaments;
    private readonly ITournamentAccess _access;

    public TournamentsController(ITournamentService tournaments, ITournamentAccess access)
    {
        _tournaments = tournaments;
        _access = access;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<TournamentDto>>> List(CancellationToken ct) =>
        Ok(await _tournaments.ListAsync(ct));

    [HttpGet("{id:int}")]
    [AllowAnonymous]
    public async Task<ActionResult<TournamentDto>> Get(int id, CancellationToken ct)
    {
        var tournament = await _tournaments.GetAsync(id, ct);
        return tournament is null ? NotFound() : Ok(tournament);
    }

    [HttpGet("{id:int}/teams")]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<TeamSummaryDto>>> Teams(int id, CancellationToken ct) =>
        Ok(await _tournaments.ListTeamsAsync(id, ct));

    /// <summary>Any signed-in account may run a tournament. Creating one makes you its auctioneer.</summary>
    [HttpPost]
    [Authorize]
    public async Task<ActionResult<TournamentDto>> Create(SaveTournamentRequest request, CancellationToken ct)
    {
        var created = await _tournaments.CreateAsync(request, _access.RequireUserId(), ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    [Authorize]
    public async Task<ActionResult<TournamentDto>> Update(int id, SaveTournamentRequest request, CancellationToken ct)
    {
        await _access.RequireManageAsync(id, ct);
        return Ok(await _tournaments.UpdateAsync(id, request, ct));
    }

    [HttpDelete("{id:int}")]
    [Authorize]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await _access.RequireManageAsync(id, ct);
        await _tournaments.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpPost("{id:int}/teams")]
    [Authorize]
    public async Task<ActionResult<TeamSummaryDto>> AddTeam(int id, SaveTeamRequest request, CancellationToken ct)
    {
        await _access.RequireManageAsync(id, ct);
        return Ok(await _tournaments.AddTeamAsync(id, request, ct));
    }

    [HttpPut("teams/{teamId:int}")]
    [Authorize]
    public async Task<ActionResult<TeamSummaryDto>> UpdateTeam(int teamId, SaveTeamRequest request, CancellationToken ct)
    {
        await _access.RequireManageTeamAsync(teamId, ct);
        return Ok(await _tournaments.UpdateTeamAsync(teamId, request, ct));
    }

    /// <summary>Adds the team's owner or a marquee player — permanent, never auctioned.</summary>
    [HttpPost("teams/{teamId:int}/members")]
    [Authorize]
    public async Task<ActionResult<TeamSummaryDto>> AddMember(
        int teamId,
        SaveTeamMemberRequest request,
        CancellationToken ct) =>
        Ok(await _tournaments.AddMemberAsync(teamId, request, ct));

    [HttpDelete("members/{playerId:int}")]
    [Authorize]
    public async Task<ActionResult<TeamSummaryDto>> RemoveMember(int playerId, CancellationToken ct) =>
        Ok(await _tournaments.RemoveMemberAsync(playerId, ct));

    [HttpDelete("teams/{teamId:int}")]
    [Authorize]
    public async Task<IActionResult> RemoveTeam(int teamId, CancellationToken ct)
    {
        await _access.RequireManageTeamAsync(teamId, ct);
        await _tournaments.RemoveTeamAsync(teamId, ct);
        return NoContent();
    }
}

[ApiController]
[Route("api/sports")]
public class SportsController : ControllerBase
{
    private readonly ITournamentService _tournaments;

    public SportsController(ITournamentService tournaments) => _tournaments = tournaments;

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<SportDto>>> List(CancellationToken ct) =>
        Ok(await _tournaments.ListSportsAsync(ct));
}
