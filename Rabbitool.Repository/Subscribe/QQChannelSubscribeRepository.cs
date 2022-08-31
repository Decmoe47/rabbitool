using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Rabbitool.Model.Entity.Subscribe;

namespace Rabbitool.Repository.Subscribe;

public class QQChannelSubscribeRepository
    : BaseRepository<QQChannelSubscribeEntity, SubscribeDbContext>
{
    public QQChannelSubscribeRepository(SubscribeDbContext ctx) : base(ctx)
    {
    }

    public async Task<QQChannelSubscribeEntity> GetAsync<TProperty>(
        string channelId,
        Expression<Func<QQChannelSubscribeEntity, TProperty>> subscribeProp,
        bool tracking = false,
        CancellationToken cancellationToken = default)
    {
        return tracking switch
        {
            false => await _dbCtx
                .QQChannelSubscribeEntity
                .AsNoTracking()
                .Include(subscribeProp)
                .Where(q => q.ChannelId == channelId)
                .FirstAsync(cancellationToken),
            true => await _dbCtx
                .QQChannelSubscribeEntity
                .Include(subscribeProp)
                .Where(q => q.ChannelId == channelId)
                .FirstAsync(cancellationToken)
        };
    }

    public async Task<QQChannelSubscribeEntity> GetAsync(
        string channelId,
        string subscribeProp,
        bool tracking = false,
        CancellationToken cancellationToken = default)
    {
        return tracking switch
        {
            false => await _dbCtx
                .QQChannelSubscribeEntity
                .AsNoTracking()
                .Include(subscribeProp)
                .Where(q => q.ChannelId == channelId)
                .FirstAsync(cancellationToken),
            true => await _dbCtx
                .QQChannelSubscribeEntity
                .Include(subscribeProp)
                .Where(q => q.ChannelId == channelId)
                .FirstAsync(cancellationToken)
        };
    }

    public async Task<QQChannelSubscribeEntity?> GetOrDefaultAsync<TProperty>(
        string channelId,
        Expression<Func<QQChannelSubscribeEntity, TProperty>> subscribeProp,
        bool tracking = false,
        CancellationToken cancellationToken = default)
    {
        return tracking switch
        {
            false => await _dbCtx
                .QQChannelSubscribeEntity
                .AsNoTracking()
                .Include(subscribeProp)
                .Where(q => q.ChannelId == channelId)
                .FirstOrDefaultAsync(cancellationToken),
            true => await _dbCtx
                .QQChannelSubscribeEntity
                .Include(subscribeProp)
                .Where(q => q.ChannelId == channelId)
                .FirstOrDefaultAsync(cancellationToken)
        };
    }

    public async Task<QQChannelSubscribeEntity?> GetOrDefaultAsync(
        string channelId,
        string subscribeProp,
        bool tracking = false,
        CancellationToken cancellationToken = default)
    {
        return tracking switch
        {
            false => await _dbCtx
                .QQChannelSubscribeEntity
                .AsNoTracking()
                .Include(subscribeProp)
                .Where(q => q.ChannelId == channelId)
                .FirstOrDefaultAsync(cancellationToken),
            true => await _dbCtx
                .QQChannelSubscribeEntity
                .Include(subscribeProp)
                .Where(q => q.ChannelId == channelId)
                .FirstOrDefaultAsync(cancellationToken)
        };
    }

    public async Task<QQChannelSubscribeEntity> CreateAsync(
        string channelId, string channelName, CancellationToken cancellationToken = default)
    {
        QQChannelSubscribeEntity record = new(channelId, channelName); ;
        await _dbCtx.QQChannelSubscribeEntity.AddAsync(record, cancellationToken);
        return record;
    }

    public async Task<QQChannelSubscribeEntity> GetOrCreateAsync<TProperty>(
        string channelId,
        string channelName,
        Expression<Func<QQChannelSubscribeEntity, TProperty>> subscribeProp,
        CancellationToken cancellationToken = default)
    {
        QQChannelSubscribeEntity? record = await GetOrDefaultAsync(channelId, subscribeProp, true, cancellationToken);
        if (record is null)
        {
            record = new QQChannelSubscribeEntity(channelId, channelName);
            await _dbCtx.QQChannelSubscribeEntity.AddAsync(record, cancellationToken);
        }
        return record;
    }

    public async Task<(QQChannelSubscribeEntity channel, bool added)> AddSubscribeAsync(
        string channelId,
        string channelName,
        ISubscribeEntity subscribe,
        CancellationToken cancellationToken = default)
    {
        bool added = true;
        QQChannelSubscribeEntity record;

        switch (subscribe)
        {
            case BilibiliSubscribeEntity s:
                record = await GetOrCreateAsync(channelId, channelName, q => q.BilibiliSubscribes, cancellationToken);
                if (record.BilibiliSubscribes is null)
                    record.BilibiliSubscribes = new List<BilibiliSubscribeEntity>() { s };
                else if (record.BilibiliSubscribes.Exists(b => b.Uid == s.Uid))
                    added = false;
                else
                    record.BilibiliSubscribes.Add(s);
                break;

            case TwitterSubscribeEntity s:
                record = await GetOrCreateAsync(channelId, channelName, q => q.TwitterSubscribes, cancellationToken);
                if (record.TwitterSubscribes is null)
                    record.TwitterSubscribes = new List<TwitterSubscribeEntity>() { s };
                else if (record.TwitterSubscribes.Exists(t => t.ScreenName == s.ScreenName))
                    added = false;
                else
                    record.TwitterSubscribes.Add(s);
                break;

            case YoutubeSubscribeEntity s:
                record = await GetOrCreateAsync(channelId, channelName, q => q.YoutubeSubscribes, cancellationToken);
                if (record.YoutubeSubscribes is null)
                    record.YoutubeSubscribes = new List<YoutubeSubscribeEntity>() { s };
                else if (record.YoutubeSubscribes.Exists(y => y.ChannelId == s.ChannelId))
                    added = false;
                else
                    record.YoutubeSubscribes.Add(s);
                break;

            case MailSubscribeEntity s:
                record = await GetOrCreateAsync(channelId, channelName, q => q.MailSubscribes, cancellationToken);
                if (record.MailSubscribes is null)
                    record.MailSubscribes = new List<MailSubscribeEntity>() { s };
                else if (record.MailSubscribes.Exists(m => m.Address == s.Address))
                    added = false;
                else
                    record.MailSubscribes.Add(s);
                break;

            default:
                throw new ArgumentException($"Unknown subscribe type {subscribe.GetType().Name}!");
        }
        return (record, added);
    }

    public async Task<QQChannelSubscribeEntity> RemoveSubscribeAsync<TProperty>(
        string channelId,
        string subscribeId,
        Expression<Func<QQChannelSubscribeEntity, TProperty>> subscribeProp,
        CancellationToken cancellationToken = default)
    {
        QQChannelSubscribeEntity record = await GetAsync(channelId, subscribeProp, true, cancellationToken);

        switch (typeof(TProperty).Name)
        {
            case nameof(BilibiliSubscribeEntity):
                if (record.BilibiliSubscribes!.FindIndex(b => b.Uid == uint.Parse(subscribeId)) is int i and not -1)
                {
                    record.BilibiliSubscribes.RemoveAt(i);
                }
                else
                {
                    throw new DataBaseRecordNotExistException(
                        $"The QQ channel (channelId: {channelId}) hasn't the bilibili subscribe (uid: {subscribeId})!");
                }

                break;

            case nameof(TwitterSubscribeEntity):
                if (record.TwitterSubscribes!.FindIndex(t => t.ScreenName == subscribeId) is int j and not -1)
                {
                    record.TwitterSubscribes.RemoveAt(j);
                }
                else
                {
                    throw new DataBaseRecordNotExistException(
                        $"The QQ channel (channelId: {channelId}) hasn't the twitter subscribe (uid: {subscribeId})!");
                }

                break;

            case nameof(YoutubeSubscribeEntity):
                if (record.YoutubeSubscribes!.FindIndex(y => y.ChannelId == subscribeId) is int k and not -1)
                {
                    record.YoutubeSubscribes.RemoveAt(k);
                }
                else
                {
                    throw new DataBaseRecordNotExistException(
                        $"The QQ channel (channelId: {channelId}) hasn't the youtube subscribe (uid: {subscribeId})!");
                }
                break;

            case nameof(MailSubscribeEntity):
                if (record.MailSubscribes!.FindIndex(m => m.Address == subscribeId) is int a and not -1)
                {
                    record.MailSubscribes.RemoveAt(a);
                }
                else
                {
                    throw new DataBaseRecordNotExistException(
                        $"The QQ channel (channelId: {channelId}) hasn't the mail subscribe (uid: {subscribeId})!");
                }
                break;
        }
        return record;
    }

    public async Task<QQChannelSubscribeEntity> RemoveSubscribeAsync(
        string channelId,
        string subscribeId,
        string subscribeProp,
        CancellationToken cancellationToken = default)
    {
        QQChannelSubscribeEntity record = await GetAsync(channelId, subscribeProp, true, cancellationToken);

        switch (subscribeProp)
        {
            case nameof(BilibiliSubscribeEntity):
                if (record.BilibiliSubscribes!.FindIndex(b => b.Uid == uint.Parse(subscribeId)) is int i and not -1)
                {
                    record.BilibiliSubscribes.RemoveAt(i);
                }
                else
                {
                    throw new DataBaseRecordNotExistException(
                        $"The QQ channel (channelId: {channelId}) hasn't the bilibili subscribe (uid: {subscribeId})!");
                }
                break;

            case nameof(TwitterSubscribeEntity):
                if (record.TwitterSubscribes!.FindIndex(t => t.ScreenName == subscribeId) is int j and not -1)
                {
                    record.TwitterSubscribes.RemoveAt(j);
                }
                else
                {
                    throw new DataBaseRecordNotExistException(
                        $"The QQ channel (channelId: {channelId}) hasn't the twitter subscribe (uid: {subscribeId})!");
                }
                break;

            case nameof(YoutubeSubscribeEntity):
                if (record.YoutubeSubscribes!.FindIndex(y => y.ChannelId == subscribeId) is int k and not -1)
                {
                    record.YoutubeSubscribes.RemoveAt(k);
                }
                else
                {
                    throw new DataBaseRecordNotExistException(
                        $"The QQ channel (channelId: {channelId}) hasn't the youtube subscribe (uid: {subscribeId})!");
                }
                break;

            case nameof(MailSubscribeEntity):
                if (record.MailSubscribes!.FindIndex(m => m.Address == subscribeId) is int a and not -1)
                {
                    record.MailSubscribes.RemoveAt(a);
                }
                else
                {
                    throw new DataBaseRecordNotExistException(
                        $"The QQ channel (channelId: {channelId}) hasn't the mail subscribe (uid: {subscribeId})!");
                }
                break;
        }
        return record;
    }
}
