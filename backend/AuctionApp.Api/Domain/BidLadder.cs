using System.Text.Json;

namespace AuctionApp.Api.Domain;

/// <summary>
/// One rung: once the standing bid reaches <see cref="From"/>, raises go up in
/// <see cref="Increment"/>.
/// </summary>
public record BidIncrementRule(decimal From, decimal Increment);

/// <summary>
/// How much a bid has to climb, which changes as the price rises — ₹1,000 steps early,
/// ₹5,000 steps once a lot is in serious money. Real auctions do not use one flat step, and
/// forcing ₹1,000 raises on a ₹80,000 player wastes everyone's evening.
/// </summary>
public static class BidLadder
{
    /// <summary>Used when an auction has no ladder configured.</summary>
    public static IReadOnlyList<BidIncrementRule> Default(decimal increment) =>
        [new BidIncrementRule(0m, increment <= 0 ? 1m : increment)];

    /// <summary>
    /// Reads a stored ladder, falling back to a single flat step. Rungs are sorted and any
    /// non-positive increment is dropped, so a malformed row can never stall the bidding by
    /// making the next minimum equal to the current bid.
    /// </summary>
    public static IReadOnlyList<BidIncrementRule> Parse(string? json, decimal fallbackIncrement)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Default(fallbackIncrement);
        }

        try
        {
            var rules = JsonSerializer.Deserialize<List<BidIncrementRule>>(json);
            var usable = rules?
                .Where(r => r.Increment > 0m && r.From >= 0m)
                .OrderBy(r => r.From)
                .ToList();

            return usable is { Count: > 0 } ? usable : Default(fallbackIncrement);
        }
        catch (JsonException)
        {
            return Default(fallbackIncrement);
        }
    }

    public static string? Serialise(IEnumerable<BidIncrementRule>? rules)
    {
        var usable = rules?
            .Where(r => r.Increment > 0m && r.From >= 0m)
            .OrderBy(r => r.From)
            .ToList();

        return usable is { Count: > 0 } ? JsonSerializer.Serialize(usable) : null;
    }

    /// <summary>The step that applies at <paramref name="standingBid"/>.</summary>
    public static decimal IncrementAt(IReadOnlyList<BidIncrementRule> ladder, decimal standingBid)
    {
        var applicable = ladder
            .Where(rule => standingBid >= rule.From)
            .OrderByDescending(rule => rule.From)
            .FirstOrDefault();

        // Below the first rung the lowest step applies, so an opening bid still has a step.
        return applicable?.Increment ?? ladder[0].Increment;
    }

    /// <summary>
    /// What the next bid has to be. With no bid yet that is the opening price; after that it
    /// is the standing bid plus whichever step the ladder is on.
    /// </summary>
    public static decimal NextMinimum(
        IReadOnlyList<BidIncrementRule> ladder,
        decimal openingPrice,
        decimal? standingBid)
    {
        if (standingBid is not decimal current)
        {
            return openingPrice;
        }

        return current + IncrementAt(ladder, current);
    }
}
