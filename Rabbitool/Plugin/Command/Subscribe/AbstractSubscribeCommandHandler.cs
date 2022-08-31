using Rabbitool.Common.Util;
using Rabbitool.Model.DTO.Command;
using Rabbitool.Model.Entity.Subscribe;
using Rabbitool.Repository.Subscribe;
using Rabbitool.Service;
using Serilog;

namespace Rabbitool.Plugin.Command.Subscribe;

public abstract class AbstractSubscribeCommandHandler<TSubscribe, TConfig, TSubscribeRepo, TConfigRepo>
    : ISubscribeCommandHandler
    where TSubscribe : ISubscribeEntity
    where TConfig : ISubscribeConfigEntity
    where TSubscribeRepo : ISubscribeRepository<TSubscribe>
    where TConfigRepo : ISubscribeConfigRepository<TSubscribe, TConfig>
{
    protected readonly LimiterUtil _limiter;
    protected readonly string _userAgent;
    protected readonly QQBotService _qbSvc;
    protected readonly SubscribeDbContext _dbCtx;
    protected readonly QQChannelSubscribeRepository _qsRepo;
    protected readonly TSubscribeRepo _repo;
    protected readonly TConfigRepo _configRepo;

    public AbstractSubscribeCommandHandler(
        QQBotService qbSvc,
        string userAgent,
        SubscribeDbContext dbCtx,
        QQChannelSubscribeRepository qsRepo,
        TSubscribeRepo repo,
        TConfigRepo configRepo)
    {
        _limiter = LimiterCollection.GetLimterBySubscribeEntity<TSubscribe>();
        _qbSvc = qbSvc;
        _dbCtx = dbCtx;
        _qsRepo = qsRepo;
        _userAgent = userAgent;
        _repo = repo;
        _configRepo = configRepo;
    }

    public abstract Task<(string name, string? errCommandMsg)> CheckId(
        string id, CancellationToken cancellationToken = default);

    public virtual async Task<string> Add(SubscribeCommandDTO command, CancellationToken cancellationToken = default)
    {
        if (command.SubscribeId is null)
            return $"请输入 {command.Platform} 对应的id！";

        (string name, string? errCommandMsg) = await CheckId(command.SubscribeId, cancellationToken);
        if (errCommandMsg is not null)
            return errCommandMsg;

        bool existEntity = true;
        bool added;
        TSubscribe record;
        QQChannelSubscribeEntity channel;

        TSubscribe? entity = await _repo.GetOrDefaultAsync(command.SubscribeId, true, cancellationToken);
        if (entity is null)
        {
            entity = SubscribeEntityHelper.NewSubscribeEntity<TSubscribe>(command.SubscribeId, name);
            await _repo.AddAsync(entity, cancellationToken);

            existEntity = false;
        }
        record = entity;

        (channel, added) = await _qsRepo.AddSubscribeAsync(
            command.QQChannel.Id, command.QQChannel.Name, record, cancellationToken);
        await _configRepo.CreateOrUpdateAsync(channel, record, command.Configs, cancellationToken);

        await _dbCtx.SaveChangesAsync(cancellationToken);

        return added && existEntity
            ? $"成功：已添加订阅到 {command.QQChannel.Name} 子频道！"
            : $"成功：已更新在 {command.QQChannel.Name} 子频道中的此订阅的配置！";
    }

    public virtual async Task<string> Delete(SubscribeCommandDTO command, CancellationToken cancellationToken = default)
    {
        if (command.SubscribeId is null)
            return $"请输入 {command.Platform} 对应的id！";

        (_, string? errCommandMsg) = await CheckId(command.SubscribeId, cancellationToken);
        if (errCommandMsg is not null)
            return errCommandMsg;

        Dictionary<string, string> logInfo = new()
        {
            { "type", typeof(TSubscribe).Name },
            { "subscribeId", command.SubscribeId },
            { "channelId", command.QQChannel.Id },
            { "channelName", command.QQChannel.Name }
        };

        try
        {
            await _qsRepo.RemoveSubscribeAsync(
                command.QQChannel.Id, command.SubscribeId, typeof(TSubscribe).Name.Replace("Entity", "s"), cancellationToken);
            await _repo.DeleteAsync(command.SubscribeId, cancellationToken);
            await _configRepo.DeleteAsync(command.QQChannel.Id, command.SubscribeId, cancellationToken);

            await _dbCtx.SaveChangesAsync(cancellationToken);
        }
        catch (InvalidOperationException iex)
        {
            Log.Error(iex, "The subscribe doesn't exist!\nInfo: {info}", logInfo);
            return "错误：不存在该订阅！";
        }

        return $"成功：已删除在 {command.QQChannel.Name} 子频道中的此订阅！";
    }

    public virtual async Task<string> List(SubscribeCommandDTO command, CancellationToken cancellationToken = default)
    {
        QQChannelSubscribeEntity channel;
        string subscribeType = typeof(TSubscribe).Name;
        string subscribeName = subscribeType.Replace("SubscribeEntity", "");
        subscribeType = subscribeType.Replace("Entity", "s");
        Dictionary<string, string> logInfo = new()
        {
            { "type", subscribeType },
            { "subscribeId", command.SubscribeId ?? "" },
            { "channelId", command.QQChannel.Id },
            { "channelName", command.QQChannel.Name }
        };

        QQChannelSubscribeEntity? record = await _qsRepo.GetOrDefaultAsync(
                command.QQChannel.Id, subscribeType, cancellationToken: cancellationToken);
        if (record is null)
        {
            Log.Warning("The channel subscribe hasn't any {subscribeType}.\nInfo: {info}", subscribeType, logInfo);
            return $"错误：{subscribeName} 还未有任何订阅！";
        }
        else
        {
            channel = record;
        }

        List<TSubscribe>? subscribes = channel.GetSubscribeProp<TSubscribe>();
        if (subscribes is null)
        {
            Log.Warning("The channel subscribe hasn't any {subscirbeType}.\nInfo: {info}", subscribeType, logInfo);
            return $"错误：{subscribeName} 还未有任何订阅！";
        }

        if (command.SubscribeId is null)
        {
            string result = "";
            foreach (TSubscribe subscribe in subscribes)
            {
                result += subscribe.GetInfo("，");
                TConfig? config = await _configRepo.GetOrDefaultAsync(
                    command.QQChannel.Id, subscribe.GetId(), cancellationToken: cancellationToken);
                if (config is not null)
                    result += "；配置：" + config.GetConfigs("，");
                result += "\n";
            }
            return result;
        }
        else
        {
            (_, string? errCommandMsg) = await CheckId(command.SubscribeId, cancellationToken);
            if (errCommandMsg is not null)
                return errCommandMsg;

            TSubscribe? subscribe = subscribes.Find(s => s.GetId() == command.SubscribeId); ;
            if (subscribe is null)
            {
                Log.Warning("The subscribe which id is {subscribeId} doesn't exist in the channel subscribe!\nInfo: {info}",
                    command.SubscribeId, command.QQChannel.Id, logInfo);
                return $"错误：id为 {command.SubscribeId} 的用户未订阅过！";
            }

            TConfig? config = await _configRepo.GetOrDefaultAsync(
                    command.QQChannel.Id, command.SubscribeId, cancellationToken: cancellationToken);
            return config is null
                ? subscribe.GetInfo("，")
                : subscribe.GetInfo("，") + "；配置：" + config.GetConfigs("，") + "\n";
        }
    }
}
