namespace AuctionApp.Api.Infrastructure;

public static class Policies
{
    /// <summary>Auction control, player CRUD, and manual bid entry.</summary>
    public const string AdminOnly = nameof(AdminOnly);
}
