using AuctionApp.Api.Contracts;
using AuctionApp.Api.Domain;
using AuctionApp.Api.Infrastructure;
using AuctionApp.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuctionApp.Api.Controllers;

[ApiController]
[Route("api/players")]
public class PlayersController : ControllerBase
{
    private readonly IPlayerService _players;
    private readonly ITournamentAccess _access;

    public PlayersController(IPlayerService players, ITournamentAccess access)
    {
        _players = players;
        _access = access;
    }

    /// <summary>The tournament's pool. Public: the viewer screen lists it without an account.</summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<PlayerDto>>> List(
        [FromQuery] int tournamentId,
        CancellationToken ct) =>
        Ok(await _players.ListAsync(tournamentId, ct));

    [HttpGet("{id:int}")]
    [AllowAnonymous]
    public async Task<ActionResult<PlayerDto>> Get(int id, CancellationToken ct)
    {
        var player = await _players.GetAsync(id, ct);
        return player is null ? NotFound() : Ok(player);
    }

    [HttpPost]
    [Authorize]
    public async Task<ActionResult<PlayerDto>> Create(SavePlayerRequest request, CancellationToken ct)
    {
        await _access.RequireManageAsync(request.TournamentId, ct);
        var created = await _players.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    [Authorize]
    public async Task<ActionResult<PlayerDto>> Update(int id, SavePlayerRequest request, CancellationToken ct)
    {
        // Both the player's current tournament and the one being moved to, so a pool
        // entry cannot be pushed into a tournament the caller does not run.
        await _access.RequireManagePlayerAsync(id, ct);
        await _access.RequireManageAsync(request.TournamentId, ct);
        return Ok(await _players.UpdateAsync(id, request, ct));
    }

    /// <summary>
    /// Replaces the player's photo. The file is re-encoded server-side into the two sizes
    /// the UI actually uses, so nothing oversized ever reaches a screen.
    /// </summary>
    [HttpPost("{id:int}/photo")]
    [Authorize]
    [RequestSizeLimit(PhotoService.MaxUploadBytes)]
    public async Task<ActionResult<PlayerDto>> UploadPhoto(int id, IFormFile file, CancellationToken ct)
    {
        await _access.RequireManagePlayerAsync(id, ct);

        if (file is null || file.Length == 0)
        {
            throw new AuctionRuleException("Choose an image to upload.");
        }

        if (file.Length > PhotoService.MaxUploadBytes)
        {
            throw new AuctionRuleException("That image is larger than the 8 MB limit.");
        }

        await using var stream = file.OpenReadStream();
        return Ok(await _players.SetPhotoAsync(id, stream, ct));
    }

    [HttpDelete("{id:int}")]
    [Authorize]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await _access.RequireManagePlayerAsync(id, ct);
        await _players.DeleteAsync(id, ct);
        return NoContent();
    }
}
