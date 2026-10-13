using System.Text;
using AuctionApp.Api.Contracts;
using AuctionApp.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace AuctionApp.Api.Controllers;

/// <summary>
/// Spectators read rooms through their share link; running one is restricted to the
/// auctioneer who owns the tournament it belongs to, or to a site admin.
/// </summary>
[ApiController]
[Route("api/auctions")]
public class AuctionsController : ControllerBase
{
    private readonly IAuctionService _auctions;
    private readonly ITournamentAccess _access;
    private readonly ICurrentUser _currentUser;
    private readonly IOptions<RetentionOptions> _retention;

    public AuctionsController(
        IAuctionService auctions,
        ITournamentAccess access,
        ICurrentUser currentUser,
        IOptions<RetentionOptions> retention)
    {
        _auctions = auctions;
        _access = access;
        _currentUser = currentUser;
        _retention = retention;
    }

    /// <summary>
    /// Every auction, including unlisted ones — so it requires an account. Spectators get
    /// the filtered <see cref="Live"/> feed instead.
    /// </summary>
    [HttpGet]
    [Authorize]
    public async Task<ActionResult<IReadOnlyList<AuctionDto>>> List(CancellationToken ct) =>
        Ok(await _auctions.ListAsync(ct));

    /// <summary>
    /// Every <em>public</em> auction currently running. This is the landing page's feed, and
    /// the reason several auctions can be watched at once. Private rooms never appear here.
    /// </summary>
    [HttpGet("live")]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<LiveAuctionCardDto>>> Live(CancellationToken ct) =>
        Ok(await _auctions.ListRunningAsync(ct));

    /// <summary>
    /// Scheduled rooms that have not opened yet. Public ones are listed for everyone; a
    /// private one appears only for the auctioneer who owns it, so the unlisted promise
    /// holds before the room opens as well as after.
    /// </summary>
    [HttpGet("upcoming")]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<LiveAuctionCardDto>>> Upcoming(CancellationToken ct) =>
        Ok(await _auctions.ListUpcomingAsync(_currentUser.UserId, _currentUser.IsAdmin, ct));

    /// <summary>How long finished auctions are kept, so the UI can state it rather than hard-code it.</summary>
    [HttpGet("retention")]
    [AllowAnonymous]
    public ActionResult<RetentionPolicyDto> Retention() =>
        Ok(new RetentionPolicyDto(_retention.Value.Enabled, _retention.Value.AuctionDays));

    /// <summary>
    /// The spectator entry point. Holding the link is the permission, so this is open to
    /// everyone and serves private rooms too.
    /// </summary>
    [HttpGet("link/{publicId:guid}/state")]
    [AllowAnonymous]
    public async Task<ActionResult<AuctionStateDto>> StateByLink(Guid publicId, CancellationToken ct)
    {
        var state = await _auctions.GetStateByLinkAsync(publicId, ct);
        return state is null ? NotFound() : Ok(state);
    }

    /// <summary>
    /// Lookup by sequential id, for the auctioneer's console and the team bidding screens.
    /// Requires an account: leaving it open would let anyone walk the id range and find
    /// unlisted rooms.
    /// </summary>
    [HttpGet("{id:int}/state")]
    [Authorize]
    public async Task<ActionResult<AuctionStateDto>> State(int id, CancellationToken ct)
    {
        var state = await _auctions.GetStateAsync(id, ct);
        return state is null ? NotFound() : Ok(state);
    }

    [HttpPost]
    [Authorize]
    public async Task<ActionResult<AuctionDto>> Create(CreateAuctionRequest request, CancellationToken ct)
    {
        await _access.RequireManageAsync(request.TournamentId, ct);
        var created = await _auctions.CreateAsync(request, ct);
        return CreatedAtAction(nameof(State), new { id = created.Id }, created);
    }

    /// <summary>
    /// The results as a spreadsheet. Open to anyone holding the share link, matching the
    /// results page it is offered from.
    /// </summary>
    [HttpGet("link/{publicId:guid}/export/{sheet}.csv")]
    [AllowAnonymous]
    public async Task<IActionResult> Export(
        Guid publicId,
        string sheet,
        [FromServices] IAuctionExportService exporter,
        CancellationToken ct)
    {
        var state = await _auctions.GetStateByLinkAsync(publicId, ct);
        if (state is null)
        {
            return NotFound();
        }

        var (csv, suffix) = sheet.ToLowerInvariant() switch
        {
            "squads" => (await exporter.SquadsCsvAsync(state.Auction.Id, ct), "squads"),
            "bids" => (await exporter.BidsCsvAsync(state.Auction.Id, ct), "bids"),
            _ => (string.Empty, string.Empty),
        };

        if (suffix.Length == 0)
        {
            return NotFound();
        }

        var name = Slug(state.Auction.Name);

        // The BOM is what makes Excel open a UTF-8 CSV with names like "Kürsat" intact
        // rather than mangling them to the system code page.
        var bytes = new byte[] { 0xEF, 0xBB, 0xBF }.Concat(Encoding.UTF8.GetBytes(csv)).ToArray();
        return File(bytes, "text/csv; charset=utf-8", $"{name}-{suffix}.csv");
    }

    /// <summary>Filename-safe form of the auction name.</summary>
    private static string Slug(string name)
    {
        var cleaned = new string(name.Select(c => char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : '-').ToArray());
        var collapsed = string.Join('-', cleaned.Split('-', StringSplitOptions.RemoveEmptyEntries));
        return collapsed.Length == 0 ? "auction" : collapsed;
    }

    /// <summary>Edits an auction. Deliberately available at any point, including mid-sale.</summary>
    [HttpPut("{id:int}")]
    [Authorize]
    public async Task<ActionResult<AuctionDto>> Update(int id, SaveAuctionRequest request, CancellationToken ct)
    {
        await _access.RequireManageAuctionAsync(id, ct);
        return Ok(await _auctions.UpdateAsync(id, request, ct));
    }

    [HttpPost("{id:int}/start")]
    [Authorize]
    public async Task<ActionResult<AuctionStateDto>> Start(int id, CancellationToken ct)
    {
        await _access.RequireManageAuctionAsync(id, ct);
        return Ok(await _auctions.StartAsync(id, ct));
    }

    [HttpPost("{id:int}/pause")]
    [Authorize]
    public async Task<ActionResult<AuctionStateDto>> Pause(int id, CancellationToken ct)
    {
        await _access.RequireManageAuctionAsync(id, ct);
        return Ok(await _auctions.PauseAsync(id, ct));
    }

    [HttpPost("{id:int}/resume")]
    [Authorize]
    public async Task<ActionResult<AuctionStateDto>> Resume(int id, CancellationToken ct)
    {
        await _access.RequireManageAuctionAsync(id, ct);
        return Ok(await _auctions.ResumeAsync(id, ct));
    }

    [HttpPost("{id:int}/end")]
    [Authorize]
    public async Task<ActionResult<AuctionStateDto>> End(int id, CancellationToken ct)
    {
        await _access.RequireManageAuctionAsync(id, ct);
        return Ok(await _auctions.EndAsync(id, ct));
    }

    /// <summary>Puts a player on the block. Bids are only accepted for this player.</summary>
    [HttpPost("{id:int}/call/{playerId:int}")]
    [Authorize]
    public async Task<ActionResult<AuctionStateDto>> Call(int id, int playerId, CancellationToken ct)
    {
        await _access.RequireManageAuctionAsync(id, ct);
        return Ok(await _auctions.PutPlayerOnBlockAsync(id, playerId, ct));
    }

    /// <summary>Hammers down the current lot to the highest bidder.</summary>
    [HttpPost("{id:int}/sell")]
    [Authorize]
    public async Task<ActionResult<AuctionStateDto>> Sell(int id, CancellationToken ct)
    {
        await _access.RequireManageAuctionAsync(id, ct);
        return Ok(await _auctions.SellCurrentPlayerAsync(id, ct));
    }

    [HttpPost("{id:int}/unsold")]
    [Authorize]
    public async Task<ActionResult<AuctionStateDto>> Unsold(int id, CancellationToken ct)
    {
        await _access.RequireManageAuctionAsync(id, ct);
        return Ok(await _auctions.MarkCurrentPlayerUnsoldAsync(id, ct));
    }

    /// <summary>
    /// Puts the last sold lot back on the block and refunds it. The way out of a hammer
    /// that fell on the wrong team.
    /// </summary>
    [HttpPost("{id:int}/reopen")]
    [Authorize]
    public async Task<ActionResult<AuctionStateDto>> Reopen(int id, CancellationToken ct)
    {
        await _access.RequireManageAuctionAsync(id, ct);
        return Ok(await _auctions.ReopenLastLotAsync(id, ct));
    }

    [HttpDelete("{id:int}")]
    [Authorize]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await _access.RequireManageAuctionAsync(id, ct);
        await _auctions.DeleteAsync(id, ct);
        return NoContent();
    }
}
