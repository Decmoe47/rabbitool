﻿using Microsoft.EntityFrameworkCore;
using Rabbitool.Model.Entity;

namespace Rabbitool.Repository;

public abstract class BaseRepository<TEntity, TDbContext>(TDbContext dbCtx) : IRepository<TEntity>
    where TEntity : class, IEntity
    where TDbContext : DbContext
{
    protected readonly TDbContext DbCtx = dbCtx;
    private bool _disposed;

    public async Task<int> SaveAsync(CancellationToken ct = default)
    {
        return await DbCtx.SaveChangesAsync(ct);
    }

    public async ValueTask DisposeAsync()
    {
        await DisposeAsync(true);
        GC.SuppressFinalize(this);
    }

    protected virtual async ValueTask DisposeAsync(bool disposing)
    {
        if (!_disposed && disposing)
            await DbCtx.DisposeAsync();
        _disposed = true;
    }
}