using System.Collections.Concurrent;

namespace AuctionApp.Api.Services;

/// <summary>
/// How many people are watching each room.
/// </summary>
/// <remarks>
/// Keyed both ways on purpose: the hub knows the auction when someone joins, but on a
/// disconnect it only has a connection id — there is no way back to the room without the
/// reverse map, and without it a dropped viewer would be counted forever.
///
/// Process-local, like the bid lock. Correct for the single-host deployment this targets;
/// several API instances would each report only their own share and want a backplane.
/// </remarks>
public interface IRoomPresence
{
    /// <summary>Records a viewer and returns the room's new count.</summary>
    int Join(string connectionId, int auctionId);

    /// <summary>Drops a viewer. Returns the room they left and its new count, or null.</summary>
    (int AuctionId, int Count)? Leave(string connectionId);

    int Count(int auctionId);
}

public class RoomPresence : IRoomPresence
{
    private readonly ConcurrentDictionary<int, ConcurrentDictionary<string, byte>> _rooms = new();
    private readonly ConcurrentDictionary<string, int> _whereConnectionIs = new();

    public int Join(string connectionId, int auctionId)
    {
        // A client that navigates between rooms on one connection leaves the old one first,
        // or it would be counted in both.
        if (_whereConnectionIs.TryGetValue(connectionId, out var previous) && previous != auctionId)
        {
            Remove(connectionId, previous);
        }

        _whereConnectionIs[connectionId] = auctionId;
        _rooms.GetOrAdd(auctionId, _ => new ConcurrentDictionary<string, byte>())[connectionId] = 0;

        return Count(auctionId);
    }

    public (int AuctionId, int Count)? Leave(string connectionId)
    {
        if (!_whereConnectionIs.TryRemove(connectionId, out var auctionId))
        {
            return null;
        }

        Remove(connectionId, auctionId);
        return (auctionId, Count(auctionId));
    }

    public int Count(int auctionId) =>
        _rooms.TryGetValue(auctionId, out var viewers) ? viewers.Count : 0;

    private void Remove(string connectionId, int auctionId)
    {
        if (!_rooms.TryGetValue(auctionId, out var viewers))
        {
            return;
        }

        viewers.TryRemove(connectionId, out _);

        // Drop the room once it empties, so a long-lived host does not accumulate an entry
        // per auction it has ever served.
        if (viewers.IsEmpty)
        {
            _rooms.TryRemove(auctionId, out _);
        }
    }
}
