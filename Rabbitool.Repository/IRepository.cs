using Rabbitool.Model.Entity;

namespace Rabbitool.Repository;

public interface IRepository<TEntity> : IAsyncDisposable
    where TEntity : IEntity
{
    Task<int> SaveAsync(CancellationToken ct = default);
}
