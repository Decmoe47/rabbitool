using Microsoft.EntityFrameworkCore;
using Rabbitool.Common.Util;
using Rabbitool.Model.DTO.Command;
using Rabbitool.Model.Entity.Subscribe;

namespace Rabbitool.Repository.Subscribe;

public class BilibiliSubscribeRepository
    : BaseRepository<BilibiliSubscribeEntity, SubscribeDbContext>,
        ISubscribeRepository<BilibiliSubscribeEntity>
{
    public BilibiliSubscribeRepository(SubscribeDbContext dbCtx) : base(dbCtx)
    {
    }

    public async Task<List<BilibiliSubscribeEntity>> GetAllAsync(bool tracking = false, CancellationToken ct = default)
    {
        return tracking switch
        {
            false => await DbCtx.BilibiliSubscribeEntity
                .AsNoTracking()
                .Include(e => e.QQChannels)
                .ToListAsync(ct),
            true => await DbCtx.BilibiliSubscribeEntity
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
        await DbCtx.BilibiliSubscribeEntity.AddAsync(entity, ct);
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
            false => await DbCtx.BilibiliSubscribeEntity
                .AsNoTracking()
                .Include(e => e.QQChannels)
                .FirstAsync(b => b.Uid == uid, ct),
            true => await DbCtx.BilibiliSubscribeEntity
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
                .BilibiliSubscribeEntity
                .AsNoTracking()
                .Include(e => e.QQChannels)
                .FirstOrDefaultAsync(b => b.Uid == uid, ct),
            true => await DbCtx.BilibiliSubscribeEntity
                .Include(e => e.QQChannels)
                .FirstOrDefaultAsync(b => b.Uid == uid, ct)
        };
    }

    public async Task<BilibiliSubscribeEntity> DeleteAsync(uint uid, CancellationToken ct = default)
    {
        BilibiliSubscribeEntity record = await GetAsync(uid, true, ct);
        DbCtx.BilibiliSubscribeEntity.Remove(record);
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

            await DbCtx.BilibiliSubscribeConfigEntity.AddAsync(record, ct);

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
                .BilibiliSubscribeConfigEntity
                .AsNoTracking()
                .Include(e => e.QQChannel)
                .Include(e => e.Subscribe)
                .Where(b => b.Subscribe.Uid == uid)
                .ToListAsync(ct),
            true => await DbCtx
                .BilibiliSubscribeConfigEntity
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
                .BilibiliSubscribeConfigEntity
                .AsNoTracking()
                .Include(e => e.QQChannel)
                .Include(e => e.Subscribe)
                .FirstAsync(b => b.Subscribe.Uid == uid && b.QQChannel.ChannelId == qqChannelId, ct),
            true => await DbCtx
                .BilibiliSubscribeConfigEntity
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
                .BilibiliSubscribeConfigEntity
                .AsNoTracking()
                .Include(e => e.QQChannel)
                .Include(e => e.Subscribe)
                .FirstOrDefaultAsync(b => b.Subscribe.Uid == uid && b.QQChannel.ChannelId == qqChannelId, ct),
            true => await DbCtx
                .BilibiliSubscribeConfigEntity
                .Include(e => e.QQChannel)
                .Include(e => e.Subscribe)
                .FirstOrDefaultAsync(b => b.Subscribe.Uid == uid && b.QQChannel.ChannelId == qqChannelId, ct)
        };
    }

    public async Task<BilibiliSubscribeConfigEntity> DeleteAsync(
        string qqChannelId, uint uid, CancellationToken ct = default)
    {
        BilibiliSubscribeConfigEntity record = await GetAsync(qqChannelId, uid, true, ct);
        DbCtx.BilibiliSubscribeConfigEntity.Remove(record);
        return record;
    }
}