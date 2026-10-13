using AuctionApp.Api.Contracts;
using AuctionApp.Api.Data;
using AuctionApp.Api.Data.Repositories;
using AuctionApp.Api.Domain;
using AuctionApp.Api.Domain.Entities;
using AuctionApp.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AuctionApp.Tests;

/// <summary>
/// Accounts are created by an admin approving a request, and the password exists in readable
/// form exactly once — in the approval response. These tests hold that shape in place.
/// </summary>
public class AccountRequestServiceTests : IDisposable
{
    private readonly AuctionDbContext _db;
    private readonly IUnitOfWork _uow;
    private readonly AccountRequestService _service;

    public AccountRequestServiceTests()
    {
        var options = new DbContextOptionsBuilder<AuctionDbContext>()
            .UseInMemoryDatabase($"account-requests-{Guid.NewGuid():N}")
            .Options;

        _db = new AuctionDbContext(options);

        _uow = new UnitOfWork(
            _db,
            new SportRepository(_db),
            new TournamentRepository(_db),
            new TeamRepository(_db),
            new PlayerRepository(_db),
            new AuctionRepository(_db),
            new BidRepository(_db),
            new UserRepository(_db));

        _service = new AccountRequestService(_db, _uow, NullLogger<AccountRequestService>.Instance);
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    private static CreateAccountRequestRequest Request(string username = "priya") =>
        new()
        {
            Username = username,
            DisplayName = "Priya R",
            AuctionName = "Floor 3 badminton",
            Contact = "@priya on Teams",
        };

    private async Task<int> SeedAdminAsync()
    {
        var admin = new User
        {
            Username = "admin",
            DisplayName = "Auction Control",
            PasswordHash = "x",
            Role = UserRole.Admin,
        };

        _db.Users.Add(admin);
        await _db.SaveChangesAsync();
        return admin.Id;
    }

    [Fact]
    public async Task A_request_starts_pending_and_creates_no_account()
    {
        var result = await _service.SubmitAsync(Request());

        Assert.Equal(AccountRequestStatus.Pending, result.Status);
        Assert.Equal("priya", result.Username);
        Assert.Empty(await _db.Users.ToListAsync());
    }

    [Fact]
    public async Task Usernames_are_lowercased_so_sign_in_is_not_case_sensitive()
    {
        var result = await _service.SubmitAsync(Request("PriYa"));

        Assert.Equal("priya", result.Username);
    }

    [Fact]
    public async Task A_second_request_for_the_same_username_is_refused()
    {
        await _service.SubmitAsync(Request());

        var error = await Assert.ThrowsAsync<AuctionRuleException>(() => _service.SubmitAsync(Request()));

        Assert.Contains("already waiting", error.Message);
    }

    [Fact]
    public async Task A_username_that_is_already_an_account_is_refused()
    {
        _db.Users.Add(new User { Username = "priya", DisplayName = "Someone", PasswordHash = "x" });
        await _db.SaveChangesAsync();

        var error = await Assert.ThrowsAsync<AuctionRuleException>(() => _service.SubmitAsync(Request()));

        Assert.Contains("already taken", error.Message);
    }

    [Fact]
    public async Task Approving_creates_an_account_whose_generated_password_actually_works()
    {
        var adminId = await SeedAdminAsync();
        var request = await _service.SubmitAsync(Request());

        var approval = await _service.ApproveAsync(request.Id, adminId);

        var created = await _db.Users.SingleAsync(u => u.Username == "priya");
        Assert.Equal(AccountRequestStatus.Approved, approval.Request.Status);
        Assert.Equal("priya", approval.Username);
        Assert.True(BCrypt.Net.BCrypt.Verify(approval.GeneratedPassword, created.PasswordHash));
        Assert.True(created.IsActive);
    }

    /// <summary>The password is read off a screen and typed into a chat, so it avoids
    /// characters that are easy to confuse.</summary>
    [Fact]
    public async Task The_generated_password_is_easy_to_read_aloud()
    {
        var adminId = await SeedAdminAsync();
        var request = await _service.SubmitAsync(Request());

        var approval = await _service.ApproveAsync(request.Id, adminId);
        var password = approval.GeneratedPassword;

        Assert.Equal(19, password.Length);
        Assert.Equal(4, password.Split('-').Length);
        Assert.All(password.Split('-'), group => Assert.Equal(4, group.Length));
        Assert.DoesNotContain(password, c => c is '0' or 'O' or '1' or 'l' or 'I');
    }

    [Fact]
    public async Task Two_approvals_never_produce_the_same_password()
    {
        var adminId = await SeedAdminAsync();
        var first = await _service.ApproveAsync((await _service.SubmitAsync(Request("one"))).Id, adminId);
        var second = await _service.ApproveAsync((await _service.SubmitAsync(Request("two"))).Id, adminId);

        Assert.NotEqual(first.GeneratedPassword, second.GeneratedPassword);
    }

    [Fact]
    public async Task A_request_cannot_be_approved_twice()
    {
        var adminId = await SeedAdminAsync();
        var request = await _service.SubmitAsync(Request());
        await _service.ApproveAsync(request.Id, adminId);

        var error = await Assert.ThrowsAsync<AuctionRuleException>(
            () => _service.ApproveAsync(request.Id, adminId));

        Assert.Contains("already been approved", error.Message);
    }

    [Fact]
    public async Task Declining_records_the_reason_and_creates_nothing()
    {
        var adminId = await SeedAdminAsync();
        var request = await _service.SubmitAsync(Request());

        var declined = await _service.DeclineAsync(request.Id, adminId, "Use the floor-2 auction instead");

        Assert.Equal(AccountRequestStatus.Declined, declined.Status);
        Assert.Equal("Use the floor-2 auction instead", declined.DecisionNote);
        Assert.Empty(await _db.Users.Where(u => u.Username == "priya").ToListAsync());
    }

    [Fact]
    public async Task Resetting_a_password_replaces_the_old_one()
    {
        var adminId = await SeedAdminAsync();
        var request = await _service.SubmitAsync(Request());
        var first = await _service.ApproveAsync(request.Id, adminId);

        var user = await _db.Users.SingleAsync(u => u.Username == "priya");
        var reset = await _service.ResetPasswordAsync(user.Id);

        var reloaded = await _db.Users.SingleAsync(u => u.Username == "priya");
        Assert.NotEqual(first.GeneratedPassword, reset.GeneratedPassword);
        Assert.False(BCrypt.Net.BCrypt.Verify(first.GeneratedPassword, reloaded.PasswordHash));
        Assert.True(BCrypt.Net.BCrypt.Verify(reset.GeneratedPassword, reloaded.PasswordHash));
    }

    [Fact]
    public async Task Pending_requests_are_listed_first()
    {
        var adminId = await SeedAdminAsync();
        var decided = await _service.SubmitAsync(Request("older"));
        await _service.DeclineAsync(decided.Id, adminId, null);
        await _service.SubmitAsync(Request("newer"));

        var all = await _service.ListAsync(status: null);

        Assert.Equal("newer", all.First().Username);
        Assert.Equal(AccountRequestStatus.Pending, all.First().Status);
    }
}
