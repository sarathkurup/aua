using AuctionApp.Api.Contracts;
using AuctionApp.Api.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace AuctionApp.Api.Services;

/// <summary>Pushes auction changes to connected clients.</summary>
public interface IAuctionNotifier
{
    /// <summary>Full room state. Sent after every mutation so all clients converge.</summary>
    Task StateChangedAsync(AuctionStateDto state, CancellationToken ct = default);

    Task BidPlacedAsync(int auctionId, BidDto bid, CancellationToken ct = default);

    Task PlayerSoldAsync(int auctionId, PlayerDto player, CancellationToken ct = default);

    Task PlayerUnsoldAsync(int auctionId, PlayerDto player, CancellationToken ct = default);

    Task AuctionStatusChangedAsync(AuctionDto auction, CancellationToken ct = default);

    /// <summary>
    /// The set of running auctions, for the public landing page. Sent to the lobby group
    /// only, so a room full of spectators watching one auction is not billed for it.
    /// </summary>
    Task LobbyChangedAsync(IReadOnlyList<LiveAuctionCardDto> running, CancellationToken ct = default);
}

public class AuctionNotifier : IAuctionNotifier
{
    private readonly IHubContext<AuctionHub> _hub;

    public AuctionNotifier(IHubContext<AuctionHub> hub) => _hub = hub;

    public Task StateChangedAsync(AuctionStateDto state, CancellationToken ct = default) =>
        Group(state.Auction.Id).SendAsync("auctionState", state, ct);

    public Task BidPlacedAsync(int auctionId, BidDto bid, CancellationToken ct = default) =>
        Group(auctionId).SendAsync("bidPlaced", bid, ct);

    public Task PlayerSoldAsync(int auctionId, PlayerDto player, CancellationToken ct = default) =>
        Group(auctionId).SendAsync("playerSold", player, ct);

    public Task PlayerUnsoldAsync(int auctionId, PlayerDto player, CancellationToken ct = default) =>
        Group(auctionId).SendAsync("playerUnsold", player, ct);

    public Task AuctionStatusChangedAsync(AuctionDto auction, CancellationToken ct = default) =>
        Group(auction.Id).SendAsync("auctionStatusChanged", auction, ct);

    public Task LobbyChangedAsync(IReadOnlyList<LiveAuctionCardDto> running, CancellationToken ct = default) =>
        _hub.Clients.Group(AuctionHub.LobbyGroup).SendAsync("lobby", running, ct);

    private IClientProxy Group(int auctionId) => _hub.Clients.Group(AuctionHub.GroupName(auctionId));
}
