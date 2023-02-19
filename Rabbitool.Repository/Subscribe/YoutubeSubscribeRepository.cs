using Microsoft.EntityFrameworkCore;
using Rabbitool.Common.Util;
using Rabbitool.Model.DTO.Command;
using Rabbitool.Model.Entity.Subscribe;

namespace Rabbitool.Repository.Subscribe;

public class YoutubeSubscribeRepository
    : BaseRepository<YoutubeSubscribeEntity, SubscribeDbContext>, ISubscribeRepository<YoutubeSubscribeEntity>
{
    public YoutubeSubscribeRepository(SubscribeDbContext ctx) : base(ctx)
    {
    }

    public async Task<List<YoutubeSubscribeEntity>> GetAllAsync(
        bool tracking = false, CancellationToken ct = default)
    {
        return tracking switch
        {
            false => await _dbCtx.YoutubeSubscribeEntity
                .AsNoTracking()
                .Include(e => e.QQChannels)
                .ToListAsync(ct),
            true => await _dbCtx.YoutubeSubscribeEntity
                .Include(e => e.QQChannels)
                .ToListAsync(ct)
        };
    }

    public async Task<YoutubeSubscribeEntity> GetAsync(
        string channelId, bool tracking = false, CancellationToken ct = default)
    {
        return tracking switch
        {
            false => await _dbCtx
                .YoutubeSubscribeEntity
                .AsNoTracking()
                .Include(e => e.QQChannels)
                .FirstAsync(y => y.ChannelId == channelId, ct),
            true => await _dbCtx.YoutubeSubscribeEntity
                .Include(e => e.QQChannels)
                .FirstAsync(y => y.ChannelId == channelId, ct)
        };
    }

    public async Task<YoutubeSubscribeEntity?> GetOrDefaultAsync(
        string channelId, bool tracking = false, CancellationToken ct = default)
    {
        return tracking switch
        {
            false => await _dbCtx
                .YoutubeSubscribeEntity
                .AsNoTracking()
                .Include(e => e.QQChannels)
                .FirstOrDefaultAsync(y => y.ChannelId == channelId, ct),
            true => await _dbCtx
                .YoutubeSubscribeEntity
                .Include(e => e.QQChannels)
                .FirstOrDefaultAsync(y => y.ChannelId == channelId, ct)
        };
    }

    public async Task AddAsync(YoutubeSubscribeEntity entity, CancellationToken ct = default)
    {
        await _dbCtx.YoutubeSubscribeEntity.AddAsync(entity, ct);
        await SaveAsync(ct);
    }

    public async Task<YoutubeSubscribeEntity> DeleteAsync(string channelId, CancellationToken ct = default)
    {
        YoutubeSubscribeEntity? record = await GetAsync(channelId, true, ct);
        _dbCtx.YoutubeSubscribeEntity.Remove(record);
        return record;
    }
}

public class YoutubeSubscribeConfigRepository
    : BaseRepository<YoutubeSubscribeConfigEntity, SubscribeDbContext>,
    ISubscribeConfigRepository<YoutubeSubscribeEntity, YoutubeSubscribeConfigEntity>
{
    public YoutubeSubscribeConfigRepository(SubscribeDbContext dbCtx) : base(dbCtx)
    {
    }

    public async Task<List<YoutubeSubscribeConfigEntity>> GetAllAsync(
        string channelId, bool tracking = false, CancellationToken ct = default)
    {
        return tracking switch
        {
            false => await _dbCtx
                .YoutubeSubscribeConfigEntity
                .AsNoTracking()
                .Include(e => e.QQChannel)
                .Include(e => e.Subscribe)
                .Where(y => y.Subscribe.ChannelId == channelId)
                .ToListAsync(ct),
            true => await _dbCtx
                .YoutubeSubscribeConfigEntity
                .Include(e => e.QQChannel)
                .Include(e => e.Subscribe)
                .Where(y => y.Subscribe.ChannelId == channelId)
                .ToListAsync(ct)
        };
    }

    public async Task<YoutubeSubscribeConfigEntity> GetAsync(
        string qqChannelId,
        string channelId,
        bool tracking = false,
        CancellationToken ct = default)
    {
        return tracking switch
        {
            false => await _dbCtx
                .YoutubeSubscribeConfigEntity
                .AsNoTracking()
                .Include(e => e.QQChannel)
                .Include(e => e.Subscribe)
                .FirstAsync(
                    y => y.QQChannel.ChannelId == qqChannelId && y.Subscribe.ChannelId == channelId, ct),
            true => await _dbCtx
                .YoutubeSubscribeConfigEntity
                .Include(e => e.QQChannel)
                .Include(e => e.Subscribe)
                .FirstAsync(
                    y => y.QQChannel.ChannelId == qqChannelId && y.Subscribe.ChannelId == channelId, ct)
        };
    }

    public async Task<YoutubeSubscribeConfigEntity?> GetOrDefaultAsync(
        string qqChannelId,
        string channelId,
        bool tracking = false,
        CancellationToken ct = default)
    {
        return tracking switch
        {
            false => await _dbCtx
                .YoutubeSubscribeConfigEntity
                .AsNoTracking()
                .Include(e => e.QQChannel)
                .Include(e => e.Subscribe)
                .FirstOrDefaultAsync(
                    y => y.QQChannel.ChannelId == qqChannelId && y.Subscribe.ChannelId == channelId, ct),
            true => await _dbCtx
                .YoutubeSubscribeConfigEntity
                .Include(e => e.QQChannel)
                .Include(e => e.Subscribe)
                .FirstOrDefaultAsync(
                    y => y.QQChannel.ChannelId == qqChannelId && y.Subscribe.ChannelId == channelId, ct)
        };
    }

    public async Task<YoutubeSubscribeConfigEntity> CreateOrUpdateAsync(
        QQChannelSubscribeEntity qqChannel,
        YoutubeSubscribeEntity subscribe,
        SubscribeConfigType? configs,
        CancellationToken ct = default)
    {
        YoutubeSubscribeConfigEntity record;

        try
        {
            record = await GetAsync(qqChannel.ChannelId, subscribe.ChannelId, true, ct);
        }
        catch (InvalidOperationException)
        {
            record = new YoutubeSubscribeConfigEntity(qqChannel, subscribe);
            if (configs is not null)
                CommonUtil.UpdateProperties(record, configs);

            await _dbCtx.YoutubeSubscribeConfigEntity.AddAsync(record, ct);

            return record;
        }
        if (configs is not null)
            CommonUtil.UpdateProperties(record, configs);
        return record;
    }

    public async Task<YoutubeSubscribeConfigEntity> DeleteAsync(
        string qqChannelId, string channelId, CancellationToken ct = default)
    {
        YoutubeSubscribeConfigEntity? record = await GetAsync(qqChannelId, channelId, true, ct);
        _dbCtx.YoutubeSubscribeConfigEntity.Remove(record);
        return record;
    }
}
