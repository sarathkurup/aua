using AuctionApp.Api.Contracts;
using AuctionApp.Api.Infrastructure;
using AuctionApp.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuctionApp.Api.Controllers;

[ApiController]
[Route("api/auctions/{auctionId:int}/bids")]
public class BidsController : ControllerBase
{
    private readonly IBidService _bids;
    private readonly ITournamentAccess _access;

    public BidsController(IBidService bids, ITournamentAccess access)
    {
        _bids = bids;
        _access = access;
    }

    /// <summary>Public: the bid feed is part of what spectators come to watch.</summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<BidDto>>> Recent(
        int auctionId,
        [FromQuery] int take,
        CancellationToken ct) =>
        Ok(await _bids.ListRecentAsync(auctionId, take <= 0 ? 15 : take, ct));

    [HttpGet("player/{playerId:int}")]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<BidDto>>> ForPlayer(int auctionId, int playerId, CancellationToken ct) =>
        Ok(await _bids.ListForPlayerAsync(auctionId, playerId, ct));

    /// <summary>A team owner bidding for their own team.</summary>
    [HttpPost]
    [Authorize]
    public async Task<ActionResult<BidDto>> Place(int auctionId, PlaceBidRequest request, CancellationToken ct) =>
        Ok(await _bids.PlaceForOwnerAsync(auctionId, User.GetUserId(), request, ct));

    /// <summary>
    /// The auctioneer entering a bid on a team's behalf — the offline path, for a bidder in
    /// the room whose device cannot reach the app.
    /// </summary>
    [HttpPost("manual")]
    [Authorize]
    public async Task<ActionResult<BidDto>> PlaceManual(int auctionId, ManualBidRequest request, CancellationToken ct)
    {
        await _access.RequireManageAuctionAsync(auctionId, ct);
        return Ok(await _bids.PlaceManualAsync(auctionId, User.GetUserId(), request, ct));
    }

    /// <summary>
    /// Takes back the standing bid on the open lot. Returns the whole room state, because
    /// the price, the leader and every team's spendable purse all move with it.
    /// </summary>
    [HttpPost("undo")]
    [Authorize]
    public async Task<ActionResult<AuctionStateDto>> Undo(int auctionId, CancellationToken ct)
    {
        await _access.RequireManageAuctionAsync(auctionId, ct);
        return Ok(await _bids.UndoLastBidAsync(auctionId, User.GetUserId(), ct));
    }
}
