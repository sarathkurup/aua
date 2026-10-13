using AuctionApp.Api.Contracts;
using AuctionApp.Api.Data.Repositories;
using AuctionApp.Api.Domain;
using AuctionApp.Api.Domain.Entities;

namespace AuctionApp.Api.Services;

public interface IAuthService
{
    /// <summary>Returns null when the credentials do not match an active account.</summary>
    Task<AuthResponse?> LoginAsync(LoginRequest request, CancellationToken ct = default);

    /// <summary>The signed-in account with its teams, for a client restoring a stored token.</summary>
    Task<UserDto?> GetProfileAsync(int userId, CancellationToken ct = default);

    /// <summary>
    /// Creates an account and signs it straight in. New accounts are always team owners —
    /// the admin role is granted out of band, never chosen at sign-up.
    /// </summary>
    Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default);

    Task<UserDto> UpdateProfileAsync(int userId, UpdateProfileRequest request, CancellationToken ct = default);
}

public class AuthService : IAuthService
{
    private readonly IUnitOfWork _uow;
    private readonly ITokenService _tokens;
    private readonly ILogger<AuthService> _logger;

    public AuthService(IUnitOfWork uow, ITokenService tokens, ILogger<AuthService> logger)
    {
        _uow = uow;
        _tokens = tokens;
        _logger = logger;
    }

    public async Task<AuthResponse?> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var user = await _uow.Users.GetByUsernameAsync(request.Username.Trim(), ct);

        // Verify against a dummy hash when the user is missing so a failed lookup and a
        // wrong password take the same time and cannot be told apart by an observer.
        if (user is null)
        {
            BCrypt.Net.BCrypt.Verify(request.Password, DummyHash);
            _logger.LogWarning("Login failed: unknown username {Username}", request.Username);
            return null;
        }

        if (!user.IsActive || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            _logger.LogWarning("Login failed for {Username}", request.Username);
            return null;
        }

        var (token, expires) = _tokens.CreateToken(user);
        var teams = await _uow.Teams.ListForOwnerAsync(user.Id, ct);
        return new AuthResponse(token, expires, user.ToDto(teams));
    }

    public async Task<UserDto?> GetProfileAsync(int userId, CancellationToken ct = default)
    {
        var user = await _uow.Users.GetByIdAsync(userId, ct);
        if (user is null || !user.IsActive) return null;

        var teams = await _uow.Teams.ListForOwnerAsync(userId, ct);
        return user.ToDto(teams);
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        var username = request.Username.Trim();

        if (await _uow.Users.GetByUsernameAsync(username, ct) is not null)
        {
            throw new AuctionRuleException($"The username '{username}' is already taken.");
        }

        var user = new User
        {
            PublicId = Guid.NewGuid(),
            Username = username,
            DisplayName = request.DisplayName.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = UserRole.TeamOwner,
            Email = Blank(request.Email),
            PhoneNumber = Blank(request.PhoneNumber),
        };

        await _uow.Users.AddAsync(user, ct);
        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation("Registered account {Username} ({PublicId})", user.Username, user.PublicId);

        var (token, expires) = _tokens.CreateToken(user);
        return new AuthResponse(token, expires, user.ToDto([]));
    }

    public async Task<UserDto> UpdateProfileAsync(
        int userId,
        UpdateProfileRequest request,
        CancellationToken ct = default)
    {
        var user = await _uow.Users.GetByIdAsync(userId, ct)
            ?? throw new NotFoundException("That account no longer exists.");

        user.DisplayName = request.DisplayName.Trim();
        user.Email = Blank(request.Email);
        user.PhoneNumber = Blank(request.PhoneNumber);
        user.AvatarUrl = Blank(request.AvatarUrl);

        await _uow.SaveChangesAsync(ct);

        var teams = await _uow.Teams.ListForOwnerAsync(userId, ct);
        return user.ToDto(teams);
    }

    /// <summary>Stores an omitted optional field as null rather than an empty string.</summary>
    private static string? Blank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private const string DummyHash = "$2a$11$N9qo8uLOickgx2ZMRZoMyeIjZAgcfl7p92ldGxad68LJZdL17lhWy";
}
