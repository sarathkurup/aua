using AuctionApp.Api.Data;
using AuctionApp.Api.Data.Repositories;
using Microsoft.Extensions.Options;

namespace AuctionApp.Api.Services;

public class RetentionOptions
{
    public const string SectionName = "Retention";

    /// <summary>Set false to keep finished auctions indefinitely.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// How long a finished auction is kept. Stated in the README as the retention promise,
    /// so changing it here means changing it there too.
    /// </summary>
    public int AuctionDays { get; set; } = 90;

    /// <summary>How often the sweep runs. Twice a day is ample for a daily-granularity rule.</summary>
    public TimeSpan SweepInterval { get; set; } = TimeSpan.FromHours(12);
}

/// <summary>
/// Enforces the retention promise: a completed auction and its bids are hard-deleted once
/// they pass the retention window.
/// </summary>
/// <remarks>
/// Only the auction event is removed. Its players stay in the tournament pool with their
/// <c>AuctionId</c> nulled by the foreign key, because a squad is the tournament's record
/// rather than the auction's, and teams would otherwise lose their rosters.
///
/// The sweep runs on a timer inside the API rather than as an external job so a single-host
/// deployment has nothing extra to install. Running several API instances would need this
/// moved behind a leader election, or they would all sweep at once — harmless but wasteful.
/// </remarks>
public class AuctionRetentionService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly RetentionOptions _options;
    private readonly ILogger<AuctionRetentionService> _logger;

    public AuctionRetentionService(
        IServiceScopeFactory scopes,
        IOptions<RetentionOptions> options,
        ILogger<AuctionRetentionService> logger)
    {
        _scopes = scopes;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Auction retention sweep is disabled by configuration.");
            return;
        }

        _logger.LogInformation(
            "Auction retention: completed auctions are deleted after {Days} days; sweeping every {Interval}.",
            _options.AuctionDays,
            _options.SweepInterval);

        // Let migrations and seeding finish before the first pass.
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        using var timer = new PeriodicTimer(_options.SweepInterval);

        do
        {
            await SweepAsync(stoppingToken);
        }
        while (await SafeWaitAsync(timer, stoppingToken));
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

            var cutoff = DateTime.UtcNow.AddDays(-_options.AuctionDays);
            var expired = await uow.Auctions.ListExpiredAsync(cutoff, ct);

            if (expired.Count == 0)
            {
                return;
            }

            foreach (var auction in expired)
            {
                // Belt and braces: a completed auction should already have an empty block,
                // and the foreign key would refuse the delete if it did not.
                auction.CurrentPlayerId = null;
                uow.Auctions.Remove(auction);
            }

            await uow.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Retention sweep deleted {Count} auction(s) completed before {Cutoff:u}.",
                expired.Count,
                cutoff);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Shutting down.
        }
        catch (Exception ex)
        {
            // A failed sweep must not take the host down; the next tick retries.
            _logger.LogError(ex, "Auction retention sweep failed. It will run again next interval.");
        }
    }
}

/// <summary>
/// Exposes the retention window so the API can state it back to clients rather than the
/// number being duplicated in the UI.
/// </summary>
public record RetentionPolicyDto(bool Enabled, int AuctionDays);
