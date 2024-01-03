using Autofac.Annotation;
using Microsoft.EntityFrameworkCore;
using Rabbitool.Common.Util;
using Rabbitool.Model.DTO.Command;
using Rabbitool.Model.Entity.Subscribe;

namespace Rabbitool.Repository.Subscribe;

[Component(AutofacScope = AutofacScope.SingleInstance)]
public class BilibiliSubscribeRepository(SubscribeDbContext dbCtx)
    : BaseRepository<BilibiliSubscribeEntity, SubscribeDbContext>(dbCtx),
        ISubscribeRepository<BilibiliSubscribeEntity>
{
    public async Task<List<BilibiliSubscribeEntity>> GetAllAsync(bool tracking = false, CancellationToken ct = default)
    {
        return tracking switch
        {
            false => await DbCtx.BilibiliSubscribes
                .AsNoTracking()
                .Include(e => e.QQChannels)
                .ToListAsync(ct),
            true => await DbCtx.BilibiliSubscribes
                .Include(e => e.QQChannels)
                .ToListAsync(ct)
        };
    }

    public async Task<BilibiliSubscribeEntity> GetAsync(
        string uid, bool tracking = false, CancellationToken ct = default)
    {
        return await GetAsync(uint.Parse(uid), tracking, ct);
    }

    public async Task<BilibiliSubscribeEntity?> GetOrDefaultAsync(
        string uid, bool tracking = false, CancellationToken ct = default)
    {
        return await GetOrDefaultAsync(uint.Parse(uid), tracking, ct);
    }

    public async Task AddAsync(BilibiliSubscribeEntity entity, CancellationToken ct = default)
    {
        await DbCtx.BilibiliSubscribes.AddAsync(entity, ct);
    }

    public async Task<BilibiliSubscribeEntity> DeleteAsync(string uid, CancellationToken ct = default)
    {
        return await DeleteAsync(uint.Parse(uid), ct);
    }

    public async Task<BilibiliSubscribeEntity> GetAsync(
        uint uid, bool tracking = false, CancellationToken ct = default)
    {
        return tracking switch
        {
            false => await DbCtx.BilibiliSubscribes
                .AsNoTracking()
                .Include(e => e.QQChannels)
                .FirstAsync(b => b.Uid == uid, ct),
            true => await DbCtx.BilibiliSubscribes
                .Include(e => e.QQChannels)
                .FirstAsync(b => b.Uid == uid, ct)
        };
    }

    public async Task<BilibiliSubscribeEntity?> GetOrDefaultAsync(
        uint uid, bool tracking = false, CancellationToken ct = default)
    {
        return tracking switch
        {
            false => await DbCtx
                .BilibiliSubscribes
                .AsNoTracking()
                .Include(e => e.QQChannels)
                .FirstOrDefaultAsync(b => b.Uid == uid, ct),
            true => await DbCtx.BilibiliSubscribes
                .Include(e => e.QQChannels)
                .FirstOrDefaultAsync(b => b.Uid == uid, ct)
        };
    }

    public async Task<BilibiliSubscribeEntity> DeleteAsync(uint uid, CancellationToken ct = default)
    {
        BilibiliSubscribeEntity record = await GetAsync(uid, true, ct);
        DbCtx.BilibiliSubscribes.Remove(record);
        return record;
    }
}

public class BilibiliSubscribeConfigRepository
    : BaseRepository<BilibiliSubscribeConfigEntity, SubscribeDbContext>,
        ISubscribeConfigRepository<BilibiliSubscribeEntity, BilibiliSubscribeConfigEntity>
{
    public BilibiliSubscribeConfigRepository(SubscribeDbContext dbCtx) : base(dbCtx)
    {
    }

    public async Task<List<BilibiliSubscribeConfigEntity>> GetAllAsync(
        string uid, bool tracking = false, CancellationToken ct = default)
    {
        return await GetAllAsync(uint.Parse(uid), tracking, ct);
    }

    public async Task<BilibiliSubscribeConfigEntity> GetAsync(
        string qqChannelId, string uid, bool tracking = false, CancellationToken ct = default)
    {
        return await GetAsync(qqChannelId, uint.Parse(uid), tracking, ct);
    }

    public async Task<BilibiliSubscribeConfigEntity?> GetOrDefaultAsync(
        string qqChannelId, string uid, bool tracking = false, CancellationToken ct = default)
    {
        return await GetOrDefaultAsync(qqChannelId, uint.Parse(uid), tracking, ct);
    }

    public async Task<BilibiliSubscribeConfigEntity> CreateOrUpdateAsync(
        QQChannelSubscribeEntity qqChannel,
        BilibiliSubscribeEntity subscribe,
        SubscribeConfigType? configs,
        CancellationToken ct = default)
    {
        BilibiliSubscribeConfigEntity record;

        try
        {
            record = await GetAsync(qqChannel.ChannelId, subscribe.Uid, true, ct);
        }
        catch (InvalidOperationException)
        {
            record = new BilibiliSubscribeConfigEntity(qqChannel, subscribe);
            if (configs != null)
                CommonUtil.UpdateProperties(record, configs);

            await DbCtx.BilibiliSubscribeConfigs.AddAsync(record, ct);

            return record;
        }

        if (configs != null)
            CommonUtil.UpdateProperties(record, configs);
        return record;
    }

    public async Task<BilibiliSubscribeConfigEntity> DeleteAsync(
        string qqChannelId, string uid, CancellationToken ct = default)
    {
        return await DeleteAsync(qqChannelId, uint.Parse(uid), ct);
    }

    public async Task<List<BilibiliSubscribeConfigEntity>> GetAllAsync(
        uint uid, bool tracking = false, CancellationToken ct = default)
    {
        return tracking switch
        {
            false => await DbCtx
                .BilibiliSubscribeConfigs
                .AsNoTracking()
                .Include(e => e.QQChannel)
                .Include(e => e.Subscribe)
                .Where(b => b.Subscribe.Uid == uid)
                .ToListAsync(ct),
            true => await DbCtx
                .BilibiliSubscribeConfigs
                .Include(e => e.QQChannel)
                .Include(e => e.Subscribe)
                .Where(b => b.Subscribe.Uid == uid)
                .ToListAsync(ct)
        };
    }

    public async Task<BilibiliSubscribeConfigEntity> GetAsync(
        string qqChannelId, uint uid, bool tracking = false, CancellationToken ct = default)
    {
        return tracking switch
        {
            false => await DbCtx
                .BilibiliSubscribeConfigs
                .AsNoTracking()
                .Include(e => e.QQChannel)
                .Include(e => e.Subscribe)
                .FirstAsync(b => b.Subscribe.Uid == uid && b.QQChannel.ChannelId == qqChannelId, ct),
            true => await DbCtx
                .BilibiliSubscribeConfigs
                .Include(e => e.QQChannel)
                .Include(e => e.Subscribe)
                .FirstAsync(b => b.Subscribe.Uid == uid && b.QQChannel.ChannelId == qqChannelId, ct)
        };
    }

    public async Task<BilibiliSubscribeConfigEntity?> GetOrDefaultAsync(
        string qqChannelId, uint uid, bool tracking = false, CancellationToken ct = default)
    {
        return tracking switch
        {
            false => await DbCtx
                .BilibiliSubscribeConfigs
                .AsNoTracking()
                .Include(e => e.QQChannel)
                .Include(e => e.Subscribe)
                .FirstOrDefaultAsync(b => b.Subscribe.Uid == uid && b.QQChannel.ChannelId == qqChannelId, ct),
            true => await DbCtx
                .BilibiliSubscribeConfigs
                .Include(e => e.QQChannel)
                .Include(e => e.Subscribe)
                .FirstOrDefaultAsync(b => b.Subscribe.Uid == uid && b.QQChannel.ChannelId == qqChannelId, ct)
        };
    }

    public async Task<BilibiliSubscribeConfigEntity> DeleteAsync(
        string qqChannelId, uint uid, CancellationToken ct = default)
    {
        BilibiliSubscribeConfigEntity record = await GetAsync(qqChannelId, uid, true, ct);
        DbCtx.BilibiliSubscribeConfigs.Remove(record);
        return record;
    }
}