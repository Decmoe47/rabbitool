using Microsoft.EntityFrameworkCore;
using Rabbitool.Model.Entity.Subscribe;

namespace Rabbitool.Repository.Subscribe;

public class QQChannelSubscribeRepository
    : BaseRepository<QQChannelSubscribeEntity, SubscribeDbContext>
{
    public QQChannelSubscribeRepository(SubscribeDbContext ctx) : base(ctx)
    {
    }

    public async Task<List<QQChannelSubscribeEntity>> GetAllAsync(
        string guildId, bool tracking = false, CancellationToken ct = default)
    {
        return tracking switch
        {
            false => await DbCtx
                .QQChannelSubscribeEntity
                .AsNoTracking()
                .Include(e => e.BilibiliSubscribes)
                .Include(e => e.YoutubeSubscribes)
                .Include(e => e.TwitterSubscribes)
                .Include(e => e.MailSubscribes)
                .Where(e => e.GuildId == guildId)
                .ToListAsync(ct),
            true => await DbCtx
                .QQChannelSubscribeEntity
                .Include(e => e.BilibiliSubscribes)
                .Include(e => e.YoutubeSubscribes)
                .Include(e => e.TwitterSubscribes)
                .Include(e => e.MailSubscribes)
                .Where(e => e.GuildId == guildId)
                .ToListAsync(ct)
        };
    }

    public async Task<QQChannelSubscribeEntity> GetAsync(
        string channelId,
        string propName,
        bool tracking = false,
        CancellationToken ct = default)
    {
        return tracking switch
        {
            false => await DbCtx
                .QQChannelSubscribeEntity
                .AsNoTracking()
                .Include(propName)
                .Where(q => q.ChannelId == channelId)
                .FirstAsync(ct),
            true => await DbCtx
                .QQChannelSubscribeEntity
                .Include(propName)
                .Where(q => q.ChannelId == channelId)
                .FirstAsync(ct)
        };
    }

    public async Task<QQChannelSubscribeEntity?> GetOrDefaultAsync(
        string channelId,
        string propName,
        bool tracking = false,
        CancellationToken ct = default)
    {
        return tracking switch
        {
            false => await DbCtx
                .QQChannelSubscribeEntity
                .AsNoTracking()
                .Include(propName)
                .Where(q => q.ChannelId == channelId)
                .FirstOrDefaultAsync(ct),
            true => await DbCtx
                .QQChannelSubscribeEntity
                .Include(propName)
                .Where(q => q.ChannelId == channelId)
                .FirstOrDefaultAsync(ct)
        };
    }

    public async Task<QQChannelSubscribeEntity> CreateAsync(
        string guildId,
        string guildName,
        string channelId,
        string channelName,
        CancellationToken ct = default)
    {
        QQChannelSubscribeEntity record = new(guildId, guildName, channelId, channelName);
        await DbCtx.QQChannelSubscribeEntity.AddAsync(record, ct);
        return record;
    }

    public async Task<QQChannelSubscribeEntity> GetOrCreateAsync(
        string guildId,
        string guildName,
        string channelId,
        string channelName,
        string propName,
        CancellationToken ct = default)
    {
        QQChannelSubscribeEntity? record = await GetOrDefaultAsync(channelId, propName, true, ct);
        if (record == null)
        {
            record = new QQChannelSubscribeEntity(guildId, guildName, channelId, channelName);
            await DbCtx.QQChannelSubscribeEntity.AddAsync(record, ct);
        }

        return record;
    }

    public async Task<(QQChannelSubscribeEntity channel, bool added)> AddSubscribeAsync<T>(
        string guildId,
        string guildName,
        string channelId,
        string channelName,
        T subscribe,
        CancellationToken ct = default) where T : ISubscribeEntity
    {
        bool added = false;
        QQChannelSubscribeEntity record = await GetOrCreateAsync(
            guildId, guildName, channelId, channelName, subscribe.PropName, ct);
        if (!record.ContainsSubscribe(subscribe))
        {
            record.AddSubscribe(subscribe);
            subscribe.QQChannels.Add(record);
            added = true;
        }

        return (record, added);
    }

    public async Task<QQChannelSubscribeEntity> RemoveSubscribeAsync<T>(
        string channelId,
        T subscribe,
        CancellationToken ct = default) where T : ISubscribeEntity
    {
        QQChannelSubscribeEntity record = await GetAsync(channelId, subscribe.PropName, true, ct);
        record.RemoveSubscribe(subscribe);
        subscribe.QQChannels.RemoveAll(q => q.ChannelId == channelId);

        return record;
    }

    public void Delete(QQChannelSubscribeEntity record)
    {
        DbCtx.QQChannelSubscribeEntity.Remove(record);
    }
}