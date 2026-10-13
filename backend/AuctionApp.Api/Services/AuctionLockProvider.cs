using System.Collections.Concurrent;

namespace AuctionApp.Api.Services;

/// <summary>
/// Serializes state changes per auction so two simultaneous bids cannot both read the
/// same standing top bid and both be accepted.
/// </summary>
/// <remarks>
/// Process-local. That is sufficient for the single-instance deployment this app targets
/// (one API host against LocalDB). Running multiple API instances against a shared
/// database would need a distributed lock or a database-level guard instead.
/// </remarks>
public interface IAuctionLockProvider
{
    Task<IAsyncDisposable> AcquireAsync(int auctionId, CancellationToken ct = default);
}

public class AuctionLockProvider : IAuctionLockProvider
{
    private readonly ConcurrentDictionary<int, SemaphoreSlim> _locks = new();

    public async Task<IAsyncDisposable> AcquireAsync(int auctionId, CancellationToken ct = default)
    {
        var gate = _locks.GetOrAdd(auctionId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        return new Releaser(gate);
    }

    private sealed class Releaser : IAsyncDisposable
    {
        private readonly SemaphoreSlim _gate;
        private bool _released;

        public Releaser(SemaphoreSlim gate) => _gate = gate;

        public ValueTask DisposeAsync()
        {
            if (!_released)
            {
                _released = true;
                _gate.Release();
            }

            return ValueTask.CompletedTask;
        }
    }
}
