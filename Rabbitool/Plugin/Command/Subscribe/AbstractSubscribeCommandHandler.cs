using System.Text.RegularExpressions;
using QQChannelFramework.Models.WsModels;
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
    protected readonly LimiterUtil _limiter = default!;
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
        if (!(typeof(TSubscribe).Name == nameof(MailSubscribeEntity)))
            _limiter = LimiterCollection.GetLimterBySubscribeEntity<TSubscribe>();
        _qbSvc = qbSvc;
        _dbCtx = dbCtx;
        _qsRepo = qsRepo;
        _userAgent = userAgent;
        _repo = repo;
        _configRepo = configRepo;
    }

    public async Task BotDeletedHandlerAsync(WsGuild guild, CancellationToken cancellationToken)
    {
        List<QQChannelSubscribeEntity> channels = await _qsRepo.GetAllAsync(guild.Id, true, cancellationToken);
        foreach (QQChannelSubscribeEntity channel in channels)
        {
            List<TSubscribe>? subscribes = channel.GetSubscribeProp<TSubscribe>();
            if (subscribes != null)
            {
                foreach (TSubscribe subscribe in subscribes)
                {
                    subscribe.RemoveQQChannel(channel.ChannelId);
                    await _configRepo.DeleteAsync(channel.ChannelId, subscribe.GetId(), cancellationToken);
                }
            }
            _qsRepo.Delete(channel);
        }

        await _dbCtx.SaveChangesAsync(cancellationToken);
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

        bool flag = true;
        bool added;
        TSubscribe record;
        QQChannelSubscribeEntity channel;

        TSubscribe? entity = await _repo.GetOrDefaultAsync(command.SubscribeId, true, cancellationToken);
        if (entity is null)
        {
            entity = SubscribeEntityHelper.NewSubscribeEntity<TSubscribe>(command.SubscribeId, name);
            await _repo.AddAsync(entity, cancellationToken);

            flag = false;
        }
        else if (entity.ContainsQQChannel(command.QQChannel.Id))
        {
            flag = false;
        }
        record = entity;

        (channel, added) = await _qsRepo.AddSubscribeAsync(
            command.QQChannel.GuildId, command.QQChannel.Id, command.QQChannel.Name, record, cancellationToken);
        await _configRepo.CreateOrUpdateAsync(channel, record, command.Configs, cancellationToken);

        await _dbCtx.SaveChangesAsync(cancellationToken);

        return added && !flag
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
            QQChannelSubscribeEntity record = await _qsRepo.RemoveSubscribeAsync(
                command.QQChannel.Id, command.SubscribeId, typeof(TSubscribe).Name.Replace("Entity", "s"), cancellationToken);
            if (record.SubscribesAreAllEmpty())
                _qsRepo.Delete(record);

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

        if (command.Configs is not null && command.Configs.TryGetValue("allChannels", out bool? allChannels) && allChannels is true)
            return await ListForAllSubscribesInGuildAsync(command, subscribeType, logInfo, cancellationToken);

        QQChannelSubscribeEntity? record = await _qsRepo.GetOrDefaultAsync(
            command.QQChannel.Id, subscribeType, cancellationToken: cancellationToken);
        if (record is null)
        {
            Log.Warning("The channel subscribe hasn't any {subscribeType}.\nInfo: {info}", subscribeType, logInfo);
            return $"错误：{command.QQChannel.Name} 子频道未有 {subscribeName} 的任何订阅！";
        }
        else
        {
            channel = record;
        }

        List<TSubscribe>? subscribes = channel.GetSubscribeProp<TSubscribe>();
        if (subscribes is null || subscribes.Count == 0)
        {
            Log.Warning("The channel subscribe hasn't any {subscirbeType}.\nInfo: {info}", subscribeType, logInfo);
            return $"错误：{command.QQChannel.Name} 子频道未有 {subscribeName} 的任何订阅！";
        }

        return command.SubscribeId is null
            ? await ListForAllSubscribesInSpecificChannelAsync(
                command.QQChannel.Id, command.QQChannel.Name, subscribes, subscribeType, logInfo, cancellationToken)
            : await ListForspecificSubscribeInspecificChannelAsync(command, subscribes, logInfo, cancellationToken);
    }

    private async Task<string> ListForAllSubscribesInGuildAsync(
        SubscribeCommandDTO command,
        string subscribeType,
        Dictionary<string, string> logInfo,
        CancellationToken cancellationToken = default)
    {
        List<QQChannelSubscribeEntity> allChannels = await _qsRepo.GetAllAsync(command.QQChannel.GuildId, cancellationToken: cancellationToken);
        if (allChannels.Count == 0)
            return "错误：当前频道的任何子频道都没有订阅！";

        List<Task<string>> tasks = new();
        foreach (QQChannelSubscribeEntity channel in allChannels)
        {
            List<TSubscribe>? subscribes = channel.GetSubscribeProp<TSubscribe>();
            if (subscribes is null || subscribes.Count == 0)
                continue;

            tasks.Add(ListForAllSubscribesInSpecificChannelAsync(
                channel.ChannelId, channel.ChannelName, subscribes, subscribeType, logInfo, cancellationToken));
        }

        if (tasks.Count == 0)
            return "错误：当前频道的任何子频道都没有订阅！";

        string[] result = await Task.WhenAll(tasks);
        return string.Join("\n\n", result);
    }

    private async Task<string> ListForAllSubscribesInSpecificChannelAsync(
        string channelId,
        string channelName,
        List<TSubscribe> subscribes,
        string subscribeType,
        Dictionary<string, string> logInfo,
        CancellationToken cancellationToken = default)
    {
        string result = "";
        foreach (TSubscribe subscribe in subscribes)
        {
            result += "- " + subscribe.GetInfo("，");
            TConfig? config = await _configRepo.GetOrDefaultAsync(
                channelId, subscribe.GetId(), cancellationToken: cancellationToken);
            if (config is not null)
                result += "；配置：" + config.GetConfigs("，");
            result += "\n";
        }

        result = Regex.Replace(
            result,
            @"[A-Za-z0-9-_\u4e00-\u9fa5]+@[a-zA-Z0-9_-]+(\.[a-zA-Z0-9_-]+)+",
            new MatchEvaluator((match) => match.ToString().Replace(".", "*")));     // 邮箱地址会被识别为链接导致无法过审

        if (result == "")
        {
            Log.Error("Unknown error!\nSubscribeType: {subscirbeType}.\nInfo: {info}", subscribeType, logInfo);
            return "错误：内部错误！";
        }
        return $"【子频道：{channelName}】\n" + result;
    }

    private async Task<string> ListForspecificSubscribeInspecificChannelAsync(
        SubscribeCommandDTO command,
        List<TSubscribe> subscribes,
        Dictionary<string, string> logInfo,
        CancellationToken cancellationToken = default)
    {
        (_, string? errCommandMsg) = await CheckId(command.SubscribeId!, cancellationToken);
        if (errCommandMsg is not null)
            return errCommandMsg;

        TSubscribe? subscribe = subscribes.Find(s => s.GetId() == command.SubscribeId); ;
        if (subscribe is null)
        {
            Log.Warning("The subscribe which id is {subscribeId} doesn't exist in the channel subscribe!\nInfo: {info}",
                command.SubscribeId, command.QQChannel.Id, logInfo);
            return $"错误：id为 {command.SubscribeId} 的用户未在 {command.QQChannel.Name} 子频道订阅过！";
        }

        TConfig? config = await _configRepo.GetOrDefaultAsync(
                command.QQChannel.Id, command.SubscribeId!, cancellationToken: cancellationToken);
        string result = config is null
            ? subscribe.GetInfo("，")
            : subscribe.GetInfo("，") + "；配置：" + config.GetConfigs("，") + "\n";

        result = Regex.Replace(
            result,
            @"[A-Za-z0-9-_\u4e00-\u9fa5]+@[a-zA-Z0-9_-]+(\.[a-zA-Z0-9_-]+)+",
            new MatchEvaluator((match) => match.ToString().Replace(".", "*")));

        return result;
    }
}
