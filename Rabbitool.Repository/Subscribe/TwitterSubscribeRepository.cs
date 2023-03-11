﻿using Microsoft.EntityFrameworkCore;
using Rabbitool.Common.Util;
using Rabbitool.Model.DTO.Command;
using Rabbitool.Model.Entity.Subscribe;

namespace Rabbitool.Repository.Subscribe;

public class TwitterSubscribeRepository
    : BaseRepository<TwitterSubscribeEntity, SubscribeDbContext>,
    ISubscribeRepository<TwitterSubscribeEntity>
{
    public TwitterSubscribeRepository(SubscribeDbContext ctx) : base(ctx)
    {
    }

    public async Task<List<TwitterSubscribeEntity>> GetAllAsync(bool tracking = false, CancellationToken ct = default)
    {
        return tracking switch
        {
            false => await _dbCtx.TwitterSubscribeEntity
                .AsNoTracking()
                .Include(e => e.QQChannels)
                .ToListAsync(ct),
            true => await _dbCtx.TwitterSubscribeEntity
                .Include(e => e.QQChannels)
                .ToListAsync(ct)
        };
    }

    public async Task<TwitterSubscribeEntity> GetAsync(
        string screenName, bool tracking = false, CancellationToken ct = default)
    {
        return tracking switch
        {
            false => await _dbCtx
                .TwitterSubscribeEntity
                .AsNoTracking()
                .Include(e => e.QQChannels)
                .FirstAsync(t => t.ScreenName == screenName, ct),
            true => await _dbCtx
                .TwitterSubscribeEntity
                .Include(e => e.QQChannels)
                .FirstAsync(t => t.ScreenName == screenName, ct)
        };
    }

    public async Task<TwitterSubscribeEntity?> GetOrDefaultAsync(
        string screenName, bool tracking = false, CancellationToken ct = default)
    {
        return tracking switch
        {
            false => await _dbCtx
                .TwitterSubscribeEntity
                .AsNoTracking()
                .Include(e => e.QQChannels)
                .FirstOrDefaultAsync(t => t.ScreenName == screenName, ct),
            true => await _dbCtx
                .TwitterSubscribeEntity
                .Include(e => e.QQChannels)
                .FirstOrDefaultAsync(t => t.ScreenName == screenName, ct)
        };
    }

    public async Task AddAsync(TwitterSubscribeEntity entity, CancellationToken ct = default)
    {
        await _dbCtx.TwitterSubscribeEntity.AddAsync(entity, ct);
    }

    public async Task<TwitterSubscribeEntity> DeleteAsync(string screenName, CancellationToken ct = default)
    {
        TwitterSubscribeEntity? record = await GetAsync(screenName, true, ct);
        _dbCtx.TwitterSubscribeEntity.Remove(record);
        return record;
    }
}

public class TwitterSubscribeConfigRepository
    : BaseRepository<TwitterSubscribeConfigEntity, SubscribeDbContext>,
    ISubscribeConfigRepository<TwitterSubscribeEntity, TwitterSubscribeConfigEntity>
{
    public TwitterSubscribeConfigRepository(SubscribeDbContext dbCtx) : base(dbCtx)
    {
    }

    public async Task<List<TwitterSubscribeConfigEntity>> GetAllAsync(
        string screenName, bool tracking = false, CancellationToken ct = default)
    {
        return tracking switch
        {
            false => await _dbCtx
                .TwitterSubscribeConfigEntity
                .AsNoTracking()
                .Include(e => e.QQChannel)
                .Include(e => e.Subscribe)
                .Where(t => t.Subscribe.ScreenName == screenName)
                .ToListAsync(ct),
            true => await _dbCtx
                .TwitterSubscribeConfigEntity
                .Include(e => e.QQChannel)
                .Include(e => e.Subscribe)
                .Where(t => t.Subscribe.ScreenName == screenName)
                .ToListAsync(ct)
        };
    }

    public async Task<TwitterSubscribeConfigEntity> GetAsync(
        string qqChannelId,
        string screenName,
        bool tracking = false,
        CancellationToken ct = default)
    {
        return tracking switch
        {
            false => await _dbCtx
                .TwitterSubscribeConfigEntity
                .AsNoTracking()
                .Include(e => e.QQChannel)
                .Include(e => e.Subscribe)
                .FirstAsync(
                    t => t.QQChannel.ChannelId == qqChannelId && t.Subscribe.ScreenName == screenName, ct),
            true => await _dbCtx
                .TwitterSubscribeConfigEntity
                .Include(e => e.QQChannel)
                .Include(e => e.Subscribe)
                .FirstAsync(
                    t => t.QQChannel.ChannelId == qqChannelId && t.Subscribe.ScreenName == screenName, ct)
        };
    }

    public async Task<TwitterSubscribeConfigEntity?> GetOrDefaultAsync(
        string qqChannelId,
        string screenName,
        bool tracking = false,
        CancellationToken ct = default)
    {
        return tracking switch
        {
            false => await _dbCtx
                .TwitterSubscribeConfigEntity
                .AsNoTracking()
                .Include(e => e.QQChannel)
                .Include(e => e.Subscribe)
                .FirstOrDefaultAsync(
                    t => t.QQChannel.ChannelId == qqChannelId && t.Subscribe.ScreenName == screenName, ct),
            true => await _dbCtx
                .TwitterSubscribeConfigEntity
                .Include(e => e.QQChannel)
                .Include(e => e.Subscribe)
                .FirstOrDefaultAsync(
                    t => t.QQChannel.ChannelId == qqChannelId && t.Subscribe.ScreenName == screenName, ct)
        };
    }

    public async Task<TwitterSubscribeConfigEntity> CreateOrUpdateAsync(
        QQChannelSubscribeEntity qqChannel,
        TwitterSubscribeEntity subscribe,
        SubscribeConfigType? configs,
        CancellationToken ct = default)
    {
        TwitterSubscribeConfigEntity record;

        try
        {
            record = await GetAsync(qqChannel.ChannelId, subscribe.ScreenName, true, ct);
        }
        catch (InvalidOperationException)
        {
            record = new TwitterSubscribeConfigEntity(qqChannel, subscribe);
            if (configs is not null)
                CommonUtil.UpdateProperties(record, configs);

            await _dbCtx.TwitterSubscribeConfigEntity.AddAsync(record, ct);

            return record;
        }
        if (configs is not null)
            CommonUtil.UpdateProperties(record, configs);
        return record;
    }

    public async Task<TwitterSubscribeConfigEntity> DeleteAsync(
        string qqChannelId, string screenName, CancellationToken ct = default)
    {
        TwitterSubscribeConfigEntity record = await GetAsync(qqChannelId, screenName, true, ct);
        _dbCtx.TwitterSubscribeConfigEntity.Remove(record);
        return record;
    }
}
