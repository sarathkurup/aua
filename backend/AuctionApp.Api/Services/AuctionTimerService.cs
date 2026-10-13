using AuctionApp.Api.Data.Repositories;
using AuctionApp.Api.Domain;

namespace AuctionApp.Api.Services;

/// <summary>
/// Closes lots whose timer has run out: sold to the standing bid, or passed if nobody bid.
/// </summary>
/// <remarks>
/// The deadline lives on the auction row, not in the auctioneer's browser. A timer held in a
/// tab stops when that tab is closed, refreshed or backgrounded, and with several people
/// watching there is no agreement on whose clock counts. Here there is exactly one deadline
/// and one thing that acts on it, and every screen simply counts down to the same instant.
///
/// Resolution goes through <see cref="IAuctionService"/> rather than touching rows directly,
/// so a timeout takes the same lock, runs the same rules and produces the same broadcast as
/// an auctioneer pressing the button.
/// </remarks>
public class AuctionTimerService : BackgroundService
{
    /// <summary>
    /// Fine enough that a lot closes within a second of its deadline, coarse enough that an
    /// idle host is not querying constantly.
    /// </summary>
    private static readonly TimeSpan Tick = TimeSpan.FromSeconds(1);

    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<AuctionTimerService> _logger;

    public AuctionTimerService(IServiceScopeFactory scopes, ILogger<AuctionTimerService> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Tick);

        while (await SafeWaitAsync(timer, stoppingToken))
        {
            await SweepAsync(stoppingToken);
        }
    }

    private static async Task<bool> SafeWaitAsync(PeriodicTimer timer, CancellationToken ct)
    {
        try
        {
            return await timer.WaitForNextTickAsync(ct);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    private async Task SweepAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopes.CreateScope();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var expired = await uow.Auctions.ListTimedOutLotsAsync(DateTime.UtcNow, ct);
            if (expired.Count == 0)
            {
                return;
            }

            var auctions = scope.ServiceProvider.GetRequiredService<IAuctionService>();

            foreach (var auctionId in expired)
            {
                await ResolveAsync(auctions, uow, auctionId, ct);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Shutting down.
        }
        catch (Exception ex)
        {
            // One bad tick must not stop the clock for every room.
            _logger.LogError(ex, "Lot timer sweep failed. It will run again on the next tick.");
        }
    }

    private async Task ResolveAsync(
        IAuctionService auctions,
        IUnitOfWork uow,
        int auctionId,
        CancellationToken ct)
    {
        try
        {
            var auction = await uow.Auctions.GetByIdAsync(auctionId, ct);
            if (auction?.CurrentPlayerId is not int playerId)
            {
                return;
            }

            // Decides sold-or-passed the same way the auctioneer would: is there a bid on the
            // table? Asking first avoids throwing an "unsold lots cannot be sold" rule error
            // and then catching it.
            var topBid = await uow.Bids.GetTopBidAsync(auctionId, playerId, ct);

            if (topBid is null)
            {
                await auctions.MarkCurrentPlayerUnsoldAsync(auctionId, ct);
                _logger.LogInformation("Lot timed out unsold in auction {AuctionId}", auctionId);
            }
            else
            {
                await auctions.SellCurrentPlayerAsync(auctionId, ct);
                _logger.LogInformation(
                    "Lot timed out and sold in auction {AuctionId} for {Amount}",
                    auctionId,
                    topBid.Amount);
            }
        }
        catch (AuctionRuleException ex)
        {
            // The auctioneer resolved it in the same instant, or the room was paused. Not an
            // error — the lot is settled either way.
            _logger.LogInformation(
                "Lot timeout for auction {AuctionId} was already handled: {Reason}",
                auctionId,
                ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not resolve the timed-out lot in auction {AuctionId}", auctionId);
        }
    }
}
