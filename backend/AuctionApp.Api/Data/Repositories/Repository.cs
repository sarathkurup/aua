using Microsoft.EntityFrameworkCore;

namespace AuctionApp.Api.Data.Repositories;

public class Repository<T> : IRepository<T>
    where T : class
{
    protected readonly AuctionDbContext Db;

    public Repository(AuctionDbContext db) => Db = db;

    protected DbSet<T> Set => Db.Set<T>();

    public virtual async Task<T?> GetByIdAsync(int id, CancellationToken ct = default) =>
        await Set.FindAsync([id], ct);

    public virtual async Task<IReadOnlyList<T>> ListAllAsync(CancellationToken ct = default) =>
        await Set.AsNoTracking().ToListAsync(ct);

    public virtual async Task AddAsync(T entity, CancellationToken ct = default) =>
        await Set.AddAsync(entity, ct);

    public virtual void Update(T entity) => Set.Update(entity);

    public virtual void Remove(T entity) => Set.Remove(entity);
}
