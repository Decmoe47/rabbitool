using Rabbitool.Model.DTO.Command;
using Rabbitool.Model.Entity.Subscribe;

namespace Rabbitool.Repository.Subscribe;

public interface ISubscribeRepository<TSubscribe> : IRepository<TSubscribe>
    where TSubscribe : ISubscribeEntity
{
    Task<TSubscribe> GetAsync(string id, bool tracking = false, CancellationToken ct = default);

    Task<TSubscribe?> GetOrDefaultAsync(string id, bool tracking = false, CancellationToken ct = default);

    Task<List<TSubscribe>> GetAllAsync(bool tracking = false, CancellationToken ct = default);

    Task AddAsync(TSubscribe entity, CancellationToken ct = default);

    Task<TSubscribe> DeleteAsync(string id, CancellationToken ct = default);
}

public interface ISubscribeConfigRepository<TSubscribe, TConfig> : IRepository<TConfig>
    where TSubscribe : ISubscribeEntity
    where TConfig : ISubscribeConfigEntity
{
    Task<List<TConfig>> GetAllAsync(string id, bool tracking = false, CancellationToken ct = default);

    Task<TConfig> GetAsync(string qqChannelId, string id, bool tracking = false, CancellationToken ct = default);

    Task<TConfig?> GetOrDefaultAsync(
        string qqChannelId, string id, bool tracking = false, CancellationToken ct = default);

    Task<TConfig> DeleteAsync(string qqChannelId, string id, CancellationToken ct = default);

    Task<TConfig> CreateOrUpdateAsync(
        QQChannelSubscribeEntity qqChannel,
        TSubscribe subscribe,
        SubscribeConfigType? configs,
        CancellationToken ct = default);
}