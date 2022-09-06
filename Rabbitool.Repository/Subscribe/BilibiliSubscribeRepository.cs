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

    public async Task<List<BilibiliSubscribeEntity>> GetAllAsync(
        bool tracking = false, CancellationToken cancellationToken = default)
    {
        return tracking switch
        {
            false => await _dbCtx.BibiliSubscribeEntity
                .AsNoTracking()
                .Include(e => e.QQChannels)
                .ToListAsync(cancellationToken),
            true => await _dbCtx.BibiliSubscribeEntity
                .Include(e => e.QQChannels)
                .ToListAsync(cancellationToken)
        };
    }

    public async Task<BilibiliSubscribeEntity> GetAsync(
        string uid, bool tracking = false, CancellationToken cancellationToken = default)
    {
        return await GetAsync(uint.Parse(uid), tracking, cancellationToken);
    }

    public async Task<BilibiliSubscribeEntity> GetAsync(
        uint uid, bool tracking = false, CancellationToken cancellationToken = default)
    {
        return tracking switch
        {
            false => await _dbCtx.BibiliSubscribeEntity
                .AsNoTracking()
                .Include(e => e.QQChannels)
                .FirstAsync(b => b.Uid == uid, cancellationToken),
            true => await _dbCtx.BibiliSubscribeEntity
                .Include(e => e.QQChannels)
                .FirstAsync(b => b.Uid == uid, cancellationToken)
        };
    }

    public async Task<BilibiliSubscribeEntity?> GetOrDefaultAsync(
        string uid, bool tracking = false, CancellationToken cancellationToken = default)
    {
        return await GetOrDefaultAsync(uint.Parse(uid), tracking, cancellationToken);
    }

    public async Task<BilibiliSubscribeEntity?> GetOrDefaultAsync(
        uint uid, bool tracking = false, CancellationToken cancellationToken = default)
    {
        return tracking switch
        {
            false => await _dbCtx
                .BibiliSubscribeEntity
                .AsNoTracking()
                .Include(e => e.QQChannels)
                .FirstOrDefaultAsync(b => b.Uid == uid, cancellationToken),
            true => await _dbCtx.BibiliSubscribeEntity
                .Include(e => e.QQChannels)
                .FirstOrDefaultAsync(b => b.Uid == uid, cancellationToken)
        };
    }

    public async Task AddAsync(BilibiliSubscribeEntity entity, CancellationToken cancellationToken = default)
    {
        await _dbCtx.BibiliSubscribeEntity.AddAsync(entity, cancellationToken);
    }

    public async Task<BilibiliSubscribeEntity> DeleteAsync(string uid, CancellationToken cancellationToken = default)
    {
        return await DeleteAsync(uint.Parse(uid), cancellationToken);
    }

    public async Task<BilibiliSubscribeEntity> DeleteAsync(uint uid, CancellationToken cancellationToken = default)
    {
        BilibiliSubscribeEntity record = await GetAsync(uid, true, cancellationToken);
        _dbCtx.BibiliSubscribeEntity.Remove(record);
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
        string uid, bool tracking = false, CancellationToken cancellationToken = default)
    {
        return await GetAllAsync(uint.Parse(uid), tracking, cancellationToken);
    }

    public async Task<List<BilibiliSubscribeConfigEntity>> GetAllAsync(
        uint uid, bool tracking = false, CancellationToken cancellationToken = default)
    {
        return tracking switch
        {
            false => await _dbCtx
                .BibiliSubscribeConfigEntity
                .AsNoTracking()
                .Include(e => e.QQChannel)
                .Include(e => e.Subscribe)
                .Where(b => b.Subscribe.Uid == uid)
                .ToListAsync(cancellationToken),
            true => await _dbCtx
                .BibiliSubscribeConfigEntity
                .Include(e => e.QQChannel)
                .Include(e => e.Subscribe)
                .Where(b => b.Subscribe.Uid == uid)
                .ToListAsync(cancellationToken)
        };
    }

    public async Task<BilibiliSubscribeConfigEntity> GetAsync(
        string qqChannelId, string uid, bool tracking = false, CancellationToken cancellationToken = default)
    {
        return await GetAsync(qqChannelId, uint.Parse(uid), tracking, cancellationToken);
    }

    public async Task<BilibiliSubscribeConfigEntity> GetAsync(
        string qqChannelId, uint uid, bool tracking = false, CancellationToken cancellationToken = default)
    {
        return tracking switch
        {
            false => await _dbCtx
                .BibiliSubscribeConfigEntity
                .AsNoTracking()
                .Include(e => e.QQChannel)
                .Include(e => e.Subscribe)
                .FirstAsync(b => b.Subscribe.Uid == uid && b.QQChannel.ChannelId == qqChannelId, cancellationToken),
            true => await _dbCtx
                .BibiliSubscribeConfigEntity
                .Include(e => e.QQChannel)
                .Include(e => e.Subscribe)
                .FirstAsync(b => b.Subscribe.Uid == uid && b.QQChannel.ChannelId == qqChannelId, cancellationToken)
        };
    }

    public async Task<BilibiliSubscribeConfigEntity?> GetOrDefaultAsync(
        string qqChannelId, string uid, bool tracking = false, CancellationToken cancellationToken = default)
    {
        return await GetOrDefaultAsync(qqChannelId, uint.Parse(uid), tracking, cancellationToken);
    }

    public async Task<BilibiliSubscribeConfigEntity?> GetOrDefaultAsync(
        string qqChannelId, uint uid, bool tracking = false, CancellationToken cancellationToken = default)
    {
        return tracking switch
        {
            false => await _dbCtx
                .BibiliSubscribeConfigEntity
                .AsNoTracking()
                .Include(e => e.QQChannel)
                .Include(e => e.Subscribe)
                .FirstOrDefaultAsync(b => b.Subscribe.Uid == uid && b.QQChannel.ChannelId == qqChannelId, cancellationToken),
            true => await _dbCtx
                .BibiliSubscribeConfigEntity
                .Include(e => e.QQChannel)
                .Include(e => e.Subscribe)
                .FirstOrDefaultAsync(b => b.Subscribe.Uid == uid && b.QQChannel.ChannelId == qqChannelId, cancellationToken)
        };
    }

    public async Task<BilibiliSubscribeConfigEntity> CreateOrUpdateAsync(
        QQChannelSubscribeEntity qqChannel,
        BilibiliSubscribeEntity subscribe,
        SubscribeConfigType? configs,
        CancellationToken cancellationToken = default)
    {
        BilibiliSubscribeConfigEntity record;

        try
        {
            record = await GetAsync(qqChannel.ChannelId, subscribe.Uid, true, cancellationToken);
        }
        catch (InvalidOperationException)
        {
            record = new BilibiliSubscribeConfigEntity(qqChannel, subscribe);
            if (configs is not null)
                CommonUtil.UpdateProperties(record, configs);

            await _dbCtx.BibiliSubscribeConfigEntity.AddAsync(record, cancellationToken);

            return record;
        }
        if (configs is not null)
            CommonUtil.UpdateProperties(record, configs);
        return record;
    }

    public async Task<BilibiliSubscribeConfigEntity> DeleteAsync(
        string qqChannelId, string uid, CancellationToken cancellationToken = default)
    {
        return await DeleteAsync(qqChannelId, uint.Parse(uid), cancellationToken);
    }

    public async Task<BilibiliSubscribeConfigEntity> DeleteAsync(
        string qqChannelId, uint uid, CancellationToken cancellationToken = default)
    {
        BilibiliSubscribeConfigEntity record = await GetAsync(qqChannelId, uid, true, cancellationToken);
        _dbCtx.BibiliSubscribeConfigEntity.Remove(record);
        return record;
    }
}
