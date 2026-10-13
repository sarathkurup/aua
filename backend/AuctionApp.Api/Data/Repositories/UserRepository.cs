using AuctionApp.Api.Domain;
using AuctionApp.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AuctionApp.Api.Data.Repositories;

public class UserRepository : Repository<User>, IUserRepository
{
    public UserRepository(AuctionDbContext db)
        : base(db)
    {
    }

    public async Task<User?> GetByUsernameAsync(string username, CancellationToken ct = default) =>
        await Set.FirstOrDefaultAsync(u => u.Username == username, ct);

    public async Task<bool> UsernameExistsAsync(string username, CancellationToken ct = default) =>
        await Set.AnyAsync(u => u.Username == username, ct);

    public async Task<IReadOnlyList<User>> ListByRoleAsync(UserRole role, CancellationToken ct = default) =>
        await Set.AsNoTracking()
            .Where(u => u.Role == role && u.IsActive)
            .OrderBy(u => u.DisplayName)
            .ToListAsync(ct);
}
