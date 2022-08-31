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

    public async Task<List<MailSubscribeEntity>> GetAllAsync(
        bool tracking = false, CancellationToken cancellationToken = default)
    {
        return tracking switch
        {
            false => await _dbCtx.MailSubscribeEntity.AsNoTracking().ToListAsync(cancellationToken),
            true => await _dbCtx.MailSubscribeEntity.ToListAsync(cancellationToken)
        };
    }

    public async Task<MailSubscribeEntity> GetAsync(
        string address, bool tracking = false, CancellationToken cancellationToken = default)
    {
        return tracking switch
        {
            false => await _dbCtx
                .MailSubscribeEntity
                .AsNoTracking()
                .FirstAsync(m => m.Address == address, cancellationToken),
            true => await _dbCtx
                .MailSubscribeEntity
                .FirstAsync(m => m.Address == address, cancellationToken)
        };
    }

    public async Task<MailSubscribeEntity?> GetOrDefaultAsync(
        string address, bool tracking = false, CancellationToken cancellationToken = default)
    {
        return tracking switch
        {
            false => await _dbCtx
                .MailSubscribeEntity
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.Address == address, cancellationToken),
            true => await _dbCtx
                .MailSubscribeEntity
                .FirstOrDefaultAsync(m => m.Address == address, cancellationToken)
        };
    }

    public async Task AddAsync(MailSubscribeEntity entity, CancellationToken cancellationToken = default)
    {
        await _dbCtx.MailSubscribeEntity.AddAsync(entity, cancellationToken);
    }

    public async Task<MailSubscribeEntity> DeleteAsync(string address, CancellationToken cancellationToken = default)
    {
        MailSubscribeEntity record = await GetAsync(address, true, cancellationToken);
        _dbCtx.MailSubscribeEntity.Remove(record);
        return record;
    }

    public async Task<MailSubscribeEntity> UpdatelastMailTimeAsync(
        string address,
        DateTime mailTime,
        CancellationToken cancellationToken = default)
    {
        MailSubscribeEntity record = await GetAsync(address, true, cancellationToken);
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
        string address, bool tracking = false, CancellationToken cancellationToken = default)
    {
        return tracking switch
        {
            false => _dbCtx
                .MailSubscribeConfigEntity
                .AsNoTracking()
                .Where(m => m.Subscribe.Address == address)
                .ToListAsync(cancellationToken),
            true => _dbCtx
                .MailSubscribeConfigEntity
                .Where(m => m.Subscribe.Address == address)
                .ToListAsync(cancellationToken)
        };
    }

    public async Task<MailSubscribeConfigEntity> GetAsync(
        string qqChannelId,
        string address,
        bool tracking = false,
        CancellationToken cancellationToken = default)
    {
        return tracking switch
        {
            false => await _dbCtx
                .MailSubscribeConfigEntity
                .AsNoTracking()
                .FirstAsync(m => m.QQChannel.ChannelId == qqChannelId && m.Subscribe.Address == address, cancellationToken),
            true => await _dbCtx
                .MailSubscribeConfigEntity
                .FirstAsync(m => m.QQChannel.ChannelId == qqChannelId && m.Subscribe.Address == address, cancellationToken)
        };
    }

    public async Task<MailSubscribeConfigEntity?> GetOrDefaultAsync(
        string qqChannelId,
        string address,
        bool tracking = false,
        CancellationToken cancellationToken = default)
    {
        return tracking switch
        {
            false => await _dbCtx
                .MailSubscribeConfigEntity
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.QQChannel.ChannelId == qqChannelId && m.Subscribe.Address == address, cancellationToken),
            true => await _dbCtx
                .MailSubscribeConfigEntity
                .FirstOrDefaultAsync(m => m.QQChannel.ChannelId == qqChannelId && m.Subscribe.Address == address, cancellationToken)
        };
    }

    public async Task<MailSubscribeConfigEntity> CreateOrUpdateAsync(
        QQChannelSubscribeEntity qqChannel,
        MailSubscribeEntity subscribe,
        SubscribeConfigType? configs,
        CancellationToken cancellationToken = default)
    {
        MailSubscribeConfigEntity record;

        try
        {
            record = await GetAsync(qqChannel.ChannelId, subscribe.Address, true, cancellationToken);
        }
        catch (InvalidOperationException)
        {
            record = new MailSubscribeConfigEntity(qqChannel, subscribe);
            await _dbCtx.MailSubscribeConfigEntity.AddAsync(record, cancellationToken);

            return record;
        }
        if (configs is not null)
            CommonUtil.UpdateProperties(record, configs);
        return record;
    }

    public async Task<MailSubscribeConfigEntity> DeleteAsync(
        string qqChannelId, string address, CancellationToken cancellationToken = default)
    {
        MailSubscribeConfigEntity record = await GetAsync(qqChannelId, address, true, cancellationToken);
        _dbCtx.MailSubscribeConfigEntity.Remove(record);
        return record;
    }
}
