using Microsoft.EntityFrameworkCore;
using Rabbitool.Model.Entity;

namespace Rabbitool.Repository;

public abstract class BaseRepository<TEntity, TDbContext> : IRepository<TEntity>
    where TEntity : class, IEntity
    where TDbContext : DbContext
{
    protected TDbContext _dbCtx;
    private bool _disposed = false;

    protected BaseRepository(TDbContext dbCtx)
    {
        _dbCtx = dbCtx;
    }

    public async Task<int> SaveAsync(CancellationToken ct = default)
    {
        return await _dbCtx.SaveChangesAsync(ct);
    }

    protected virtual async ValueTask DisposeAsync(bool disposing)
    {
        if (!_disposed && disposing)
            await _dbCtx.DisposeAsync();
        _disposed = true;
    }

    public async ValueTask DisposeAsync()
    {
        await DisposeAsync(true);
        GC.SuppressFinalize(this);
    }
}
