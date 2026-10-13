using AuctionApp.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace AuctionApp.Api.Hubs;

/// <summary>
/// Real-time channel for the auction room. Clients join a per-auction group and
/// receive the full room state after every change, so a reconnecting device
/// converges without replaying missed events.
///
/// Anonymous by design: spectators watch without an account. The hub is read-only —
/// it has no method that changes anything, and every write goes through an
/// authenticated REST endpoint — so opening it up grants no more than the public
/// viewer screen already shows.
/// </summary>
[AllowAnonymous]
public class AuctionHub : Hub
{
    private readonly IAuctionService _auctions;
    private readonly IRoomPresence _presence;
    private readonly ILogger<AuctionHub> _logger;

    public AuctionHub(IAuctionService auctions, IRoomPresence presence, ILogger<AuctionHub> logger)
    {
        _auctions = auctions;
        _presence = presence;
        _logger = logger;
    }

    public static string GroupName(int auctionId) => $"auction-{auctionId}";

    /// <summary>The group every client watching the lobby joins.</summary>
    public const string LobbyGroup = "lobby";

    /// <summary>
    /// Subscribe to a room by its share link and immediately receive the current state.
    ///
    /// Clients join by <see cref="Domain.Entities.Auction.PublicId"/>, never by the
    /// sequential id: an unguessable key is what keeps a private room private, and a
    /// caller who cannot produce one gets nothing back.
    /// </summary>
    public async Task JoinAuction(Guid publicId)
    {
        var state = await _auctions.GetStateByLinkAsync(publicId, Context.ConnectionAborted);
        if (state is null)
        {
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(state.Auction.Id));
        await Clients.Caller.SendAsync("auctionState", state, Context.ConnectionAborted);

        var viewers = _presence.Join(Context.ConnectionId, state.Auction.Id);
        await Clients.Group(GroupName(state.Auction.Id)).SendAsync("viewers", viewers);
    }

    public async Task LeaveAuction(Guid publicId)
    {
        var state = await _auctions.GetStateByLinkAsync(publicId, Context.ConnectionAborted);
        if (state is not null)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(state.Auction.Id));
        }

        await ReleasePresenceAsync();
    }

    /// <summary>
    /// A cheer on the current lot. Signed in only — an anonymous firehose would be noise,
    /// and the room already has a hundred spectators who did not have to identify themselves.
    ///
    /// Deliberately not persisted: a reaction is a moment, not a record. It is relayed to
    /// whoever is in the room at the time and then forgotten.
    /// </summary>
    [Authorize]
    public async Task SendReaction(Guid publicId, string emoji)
    {
        if (!AllowedReactions.Contains(emoji))
        {
            return;
        }

        var state = await _auctions.GetStateByLinkAsync(publicId, Context.ConnectionAborted);
        if (state is null)
        {
            return;
        }

        await Clients.Group(GroupName(state.Auction.Id)).SendAsync(
            "reaction",
            new { emoji, from = Context.User?.FindFirst("displayName")?.Value ?? "Someone" });
    }

    /// <summary>Fixed set, so the relay can never be used to push arbitrary text to a room.</summary>
    private static readonly HashSet<string> AllowedReactions = ["🔥", "👏", "😮", "🎉", "💰"];

    /// <summary>
    /// Watch the set of running auctions rather than one room. Used by the public landing
    /// page, which has to notice a new auction opening without polling.
    /// </summary>
    public async Task JoinLobby()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, LobbyGroup);

        var running = await _auctions.ListRunningAsync(Context.ConnectionAborted);
        await Clients.Caller.SendAsync("lobby", running, Context.ConnectionAborted);
    }

    public Task LeaveLobby() => Groups.RemoveFromGroupAsync(Context.ConnectionId, LobbyGroup);

    public override Task OnConnectedAsync()
    {
        _logger.LogInformation(
            "Auction hub connected: {ConnectionId} ({User})",
            Context.ConnectionId,
            Context.User?.Identity?.Name ?? "spectator");
        return base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Auction hub disconnected: {ConnectionId}", Context.ConnectionId);

        // Without this a closed tab is counted as a viewer forever.
        await ReleasePresenceAsync();
        await base.OnDisconnectedAsync(exception);
    }

    private async Task ReleasePresenceAsync()
    {
        if (_presence.Leave(Context.ConnectionId) is not var (auctionId, viewers))
        {
            return;
        }

        await Clients.Group(GroupName(auctionId)).SendAsync("viewers", viewers);
    }
}
