using Microsoft.EntityFrameworkCore;
using Rabbitool.Common.Util;
using Rabbitool.Model.DTO.Command;
using Rabbitool.Model.Entity.Subscribe;

namespace Rabbitool.Repository.Subscribe;

public class MailSubscribeRepository
    : BaseRepository<MailSubscribeEntity, SubscribeDbContext>,
    ISubscribeRepository<MailSubscribeEntity>
{
    public MailSubscribeRepository(SubscribeDbContext dbCtx) : base(dbCtx)
    {
    }

    public async Task<List<MailSubscribeEntity>> GetAllAsync(bool tracking = false, CancellationToken ct = default)
    {
        return tracking switch
        {
            false => await _dbCtx.MailSubscribeEntity
                .AsNoTracking()
                .Include(e => e.QQChannels)
                .ToListAsync(ct),
            true => await _dbCtx.MailSubscribeEntity
                .Include(e => e.QQChannels)
                .ToListAsync(ct)
        };
    }

    public async Task<MailSubscribeEntity> GetAsync(
        string address, bool tracking = false, CancellationToken ct = default)
    {
        return tracking switch
        {
            false => await _dbCtx
                .MailSubscribeEntity
                .AsNoTracking()
                .Include(e => e.QQChannels)
                .FirstAsync(m => m.Address == address, ct),
            true => await _dbCtx
                .MailSubscribeEntity
                .Include(e => e.QQChannels)
                .FirstAsync(m => m.Address == address, ct)
        };
    }

    public async Task<MailSubscribeEntity?> GetOrDefaultAsync(
        string address, bool tracking = false, CancellationToken ct = default)
    {
        return tracking switch
        {
            false => await _dbCtx
                .MailSubscribeEntity
                .AsNoTracking()
                .Include(e => e.QQChannels)
                .FirstOrDefaultAsync(m => m.Address == address, ct),
            true => await _dbCtx
                .MailSubscribeEntity
                .Include(e => e.QQChannels)
                .FirstOrDefaultAsync(m => m.Address == address, ct)
        };
    }

    public async Task AddAsync(MailSubscribeEntity entity, CancellationToken ct = default)
    {
        await _dbCtx.MailSubscribeEntity.AddAsync(entity, ct);
    }

    public async Task<MailSubscribeEntity> DeleteAsync(string address, CancellationToken ct = default)
    {
        MailSubscribeEntity record = await GetAsync(address, true, ct);
        _dbCtx.MailSubscribeEntity.Remove(record);
        return record;
    }

    public async Task<MailSubscribeEntity> UpdatelastMailTimeAsync(
        string address,
        DateTime mailTime,
        CancellationToken ct = default)
    {
        MailSubscribeEntity record = await GetAsync(address, true, ct);
        record.LastMailTime = mailTime;
        return record;
    }
}

public class MailSubscribeConfigRepository
    : BaseRepository<MailSubscribeConfigEntity, SubscribeDbContext>,
    ISubscribeConfigRepository<MailSubscribeEntity, MailSubscribeConfigEntity>
{
    public MailSubscribeConfigRepository(SubscribeDbContext dbCtx) : base(dbCtx)
    {
    }

    public Task<List<MailSubscribeConfigEntity>> GetAllAsync(
        string address, bool tracking = false, CancellationToken ct = default)
    {
        return tracking switch
        {
            false => _dbCtx
                .MailSubscribeConfigEntity
                .AsNoTracking()
                .Include(e => e.QQChannel)
                .Include(e => e.Subscribe)
                .Where(m => m.Subscribe.Address == address)
                .ToListAsync(ct),
            true => _dbCtx
                .MailSubscribeConfigEntity
                .Include(e => e.QQChannel)
                .Include(e => e.Subscribe)
                .Where(m => m.Subscribe.Address == address)
                .ToListAsync(ct)
        };
    }

    public async Task<MailSubscribeConfigEntity> GetAsync(
        string qqChannelId,
        string address,
        bool tracking = false,
        CancellationToken ct = default)
    {
        return tracking switch
        {
            false => await _dbCtx
                .MailSubscribeConfigEntity
                .AsNoTracking()
                .Include(e => e.QQChannel)
                .Include(e => e.Subscribe)
                .FirstAsync(m => m.QQChannel.ChannelId == qqChannelId && m.Subscribe.Address == address, ct),
            true => await _dbCtx
                .MailSubscribeConfigEntity
                .Include(e => e.QQChannel)
                .Include(e => e.Subscribe)
                .FirstAsync(m => m.QQChannel.ChannelId == qqChannelId && m.Subscribe.Address == address, ct)
        };
    }

    public async Task<MailSubscribeConfigEntity?> GetOrDefaultAsync(
        string qqChannelId,
        string address,
        bool tracking = false,
        CancellationToken ct = default)
    {
        return tracking switch
        {
            false => await _dbCtx
                .MailSubscribeConfigEntity
                .AsNoTracking()
                .Include(e => e.QQChannel)
                .Include(e => e.Subscribe)
                .FirstOrDefaultAsync(m => m.QQChannel.ChannelId == qqChannelId && m.Subscribe.Address == address, ct),
            true => await _dbCtx
                .MailSubscribeConfigEntity
                .Include(e => e.QQChannel)
                .Include(e => e.Subscribe)
                .FirstOrDefaultAsync(m => m.QQChannel.ChannelId == qqChannelId && m.Subscribe.Address == address, ct)
        };
    }

    public async Task<MailSubscribeConfigEntity> CreateOrUpdateAsync(
        QQChannelSubscribeEntity qqChannel,
        MailSubscribeEntity subscribe,
        SubscribeConfigType? configs,
        CancellationToken ct = default)
    {
        MailSubscribeConfigEntity record;

        try
        {
            record = await GetAsync(qqChannel.ChannelId, subscribe.Address, true, ct);
        }
        catch (InvalidOperationException)
        {
            record = new MailSubscribeConfigEntity(qqChannel, subscribe);
            if (configs != null)
                CommonUtil.UpdateProperties(record, configs);

            await _dbCtx.MailSubscribeConfigEntity.AddAsync(record, ct);

            return record;
        }
        if (configs != null)
            CommonUtil.UpdateProperties(record, configs);
        return record;
    }

    public async Task<MailSubscribeConfigEntity> DeleteAsync(
        string qqChannelId, string address, CancellationToken ct = default)
    {
        MailSubscribeConfigEntity record = await GetAsync(qqChannelId, address, true, ct);
        _dbCtx.MailSubscribeConfigEntity.Remove(record);
        return record;
    }
}
