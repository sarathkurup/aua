using System.Security.Cryptography;
using AuctionApp.Api.Contracts;
using AuctionApp.Api.Data;
using AuctionApp.Api.Data.Repositories;
using AuctionApp.Api.Domain;
using AuctionApp.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AuctionApp.Api.Services;

public interface IAccountRequestService
{
    /// <summary>Anyone in the office asking for an auction of their own.</summary>
    Task<AccountRequestDto> SubmitAsync(CreateAccountRequestRequest request, CancellationToken ct = default);

    Task<IReadOnlyList<AccountRequestDto>> ListAsync(AccountRequestStatus? status, CancellationToken ct = default);

    /// <summary>
    /// Creates the account and returns its generated password — the only time it is readable.
    /// </summary>
    Task<ApprovalResultDto> ApproveAsync(int requestId, int adminUserId, CancellationToken ct = default);

    Task<AccountRequestDto> DeclineAsync(
        int requestId,
        int adminUserId,
        string? reason,
        CancellationToken ct = default);

    /// <summary>Mints a fresh password for an existing account when the old one is lost.</summary>
    Task<ApprovalResultDto> ResetPasswordAsync(int userId, CancellationToken ct = default);
}

public class AccountRequestService : IAccountRequestService
{
    private readonly AuctionDbContext _db;
    private readonly IUnitOfWork _uow;
    private readonly ILogger<AccountRequestService> _logger;

    public AccountRequestService(AuctionDbContext db, IUnitOfWork uow, ILogger<AccountRequestService> logger)
    {
        _db = db;
        _uow = uow;
        _logger = logger;
    }

    public async Task<AccountRequestDto> SubmitAsync(
        CreateAccountRequestRequest request,
        CancellationToken ct = default)
    {
        var username = request.Username.Trim().ToLowerInvariant();

        // Checked here for a helpful message, and again at approval, because the name could
        // be taken in between by another approval.
        if (await _uow.Users.UsernameExistsAsync(username, ct))
        {
            throw new AuctionRuleException($"The username '{username}' is already taken. Pick another.");
        }

        if (await _db.AccountRequests.AnyAsync(
                r => r.Username == username && r.Status == AccountRequestStatus.Pending, ct))
        {
            throw new AuctionRuleException($"A request for '{username}' is already waiting for a decision.");
        }

        var entity = new AccountRequest
        {
            Username = username,
            DisplayName = request.DisplayName.Trim(),
            AuctionName = request.AuctionName?.Trim(),
            Contact = request.Contact?.Trim(),
            Status = AccountRequestStatus.Pending,
        };

        _db.AccountRequests.Add(entity);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Account requested: {Username} ({DisplayName})", username, entity.DisplayName);
        return ToDto(entity);
    }

    public async Task<IReadOnlyList<AccountRequestDto>> ListAsync(
        AccountRequestStatus? status,
        CancellationToken ct = default)
    {
        var query = _db.AccountRequests
            .AsNoTracking()
            .Include(r => r.DecidedByUser)
            .AsQueryable();

        if (status.HasValue)
        {
            query = query.Where(r => r.Status == status.Value);
        }

        // Pending first — the console is a queue of decisions, not a history.
        var rows = await query
            .OrderBy(r => r.Status == AccountRequestStatus.Pending ? 0 : 1)
            .ThenByDescending(r => r.RequestedAt)
            .ToListAsync(ct);

        return rows.Select(ToDto).ToList();
    }

    public async Task<ApprovalResultDto> ApproveAsync(
        int requestId,
        int adminUserId,
        CancellationToken ct = default)
    {
        var request = await _db.AccountRequests.FirstOrDefaultAsync(r => r.Id == requestId, ct)
            ?? throw new NotFoundException($"Request {requestId} was not found.");

        if (request.Status != AccountRequestStatus.Pending)
        {
            throw new AuctionRuleException($"That request has already been {request.Status.ToString().ToLowerInvariant()}.");
        }

        if (await _uow.Users.UsernameExistsAsync(request.Username, ct))
        {
            throw new AuctionRuleException(
                $"The username '{request.Username}' was taken while this request was waiting. " +
                "Decline it and ask for another name.");
        }

        var password = GeneratePassword();

        var user = new User
        {
            Username = request.Username,
            DisplayName = request.DisplayName,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            // Approved accounts run their own auctions. Whoever creates a tournament owns it,
            // so this is enough authority without making everyone a site admin.
            Role = UserRole.TeamOwner,
            IsActive = true,
        };

        await _uow.Users.AddAsync(user, ct);
        await _uow.SaveChangesAsync(ct);

        request.Status = AccountRequestStatus.Approved;
        request.DecidedAt = DateTime.UtcNow;
        request.DecidedByUserId = adminUserId;
        request.CreatedUserId = user.Id;
        await _db.SaveChangesAsync(ct);

        // Deliberately not logged: the log would outlive the one-time display.
        _logger.LogInformation(
            "Account request {RequestId} approved by {AdminId}; account {UserId} created",
            requestId,
            adminUserId,
            user.Id);

        return new ApprovalResultDto(ToDto(request), user.Username, password);
    }

    public async Task<AccountRequestDto> DeclineAsync(
        int requestId,
        int adminUserId,
        string? reason,
        CancellationToken ct = default)
    {
        var request = await _db.AccountRequests.FirstOrDefaultAsync(r => r.Id == requestId, ct)
            ?? throw new NotFoundException($"Request {requestId} was not found.");

        if (request.Status != AccountRequestStatus.Pending)
        {
            throw new AuctionRuleException("That request has already been decided.");
        }

        request.Status = AccountRequestStatus.Declined;
        request.DecidedAt = DateTime.UtcNow;
        request.DecidedByUserId = adminUserId;
        request.DecisionNote = reason?.Trim();
        await _db.SaveChangesAsync(ct);

        return ToDto(request);
    }

    public async Task<ApprovalResultDto> ResetPasswordAsync(int userId, CancellationToken ct = default)
    {
        var user = await _uow.Users.GetByIdAsync(userId, ct)
            ?? throw new NotFoundException($"User {userId} was not found.");

        var password = GeneratePassword();
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password);
        await _uow.SaveChangesAsync(ct);

        _logger.LogWarning("Password reset for account {UserId} ({Username})", user.Id, user.Username);

        return new ApprovalResultDto(
            new AccountRequestDto(0, user.Username, user.DisplayName, null, null,
                AccountRequestStatus.Approved, user.CreatedAt, DateTime.UtcNow, null, null),
            user.Username,
            password);
    }

    /// <summary>
    /// A password meant to be read off a screen and typed into a chat window: four groups of
    /// four from an alphabet with no 0/O or 1/l/I, so it survives being copied by eye.
    /// </summary>
    private static string GeneratePassword()
    {
        const string alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
        var characters = new char[19];

        for (var i = 0; i < characters.Length; i++)
        {
            characters[i] = i is 4 or 9 or 14
                ? '-'
                : alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
        }

        return new string(characters);
    }

    private static AccountRequestDto ToDto(AccountRequest request) =>
        new(
            request.Id,
            request.Username,
            request.DisplayName,
            request.AuctionName,
            request.Contact,
            request.Status,
            request.RequestedAt,
            request.DecidedAt,
            request.DecidedByUser?.DisplayName,
            request.DecisionNote);
}
