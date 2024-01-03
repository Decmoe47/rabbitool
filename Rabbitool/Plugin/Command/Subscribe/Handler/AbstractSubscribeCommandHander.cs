using System.Text.RegularExpressions;
using Flurl.Http;
using MyBot.Models.WsModels;
using Rabbitool.Model.DTO.Command;
using Rabbitool.Model.Entity.Subscribe;
using Rabbitool.Repository.Subscribe;
using Serilog;

namespace Rabbitool.Plugin.Command.Subscribe.Handler;

public abstract partial class AbstractSubscribeCommandHandler<TSubscribe, TConfig, TSubscribeRepo, TConfigRepo>(
    SubscribeDbContext dbCtx,
    QQChannelSubscribeRepository qsRepo,
    TSubscribeRepo repo,
    TConfigRepo configRepo) : ISubscribeCommandHandler
    where TSubscribe : ISubscribeEntity
    where TConfig : ISubscribeConfigEntity
    where TSubscribeRepo : ISubscribeRepository<TSubscribe>
    where TConfigRepo : ISubscribeConfigRepository<TSubscribe, TConfig>
{
    public async Task BotDeletedHandlerAsync(WsGuild guild, CancellationToken ct = default)
    {
        List<QQChannelSubscribeEntity> channels = await qsRepo.GetAllAsync(guild.Id, true, ct);
        foreach (QQChannelSubscribeEntity channel in channels)
        {
            List<TSubscribe>? subscribes = channel.GetSubscribeProp<TSubscribe>();
            if (subscribes != null)
                foreach (TSubscribe subscribe in subscribes)
                {
                    subscribe.QQChannels.RemoveAll(q => q.ChannelId == channel.ChannelId);
                    await configRepo.DeleteAsync(channel.ChannelId, subscribe.GetId(), ct);
                }

            qsRepo.Delete(channel);
        }

        await dbCtx.SaveChangesAsync(ct);
    }

    public abstract Task<(string name, string? errMsg)> CheckId(string id, CancellationToken ct = default);

    public virtual async Task<string> Add(SubscribeCommand cmd, CancellationToken ct = default)
    {
        if (cmd.SubscribeId == null)
            return $"请输入 {cmd.Platform} 对应的id！";

        string name;
        string? errMsg;
        try
        {
            (name, errMsg) = await CheckId(cmd.SubscribeId, ct);
        }
        catch (FlurlHttpException ex)
        {
            Log.Error(ex, "Failed to check id {id}", cmd.SubscribeId);
            return "在检查id时发生http错误！";
        }

        if (errMsg != null)
            return errMsg;

        bool flag = true;
        TSubscribe? record = await repo.GetOrDefaultAsync(cmd.SubscribeId, true, ct);
        if (record == null)
        {
            record = SubscribeEntityHelper.NewSubscribeEntity<TSubscribe>(cmd.SubscribeId, name);
            await repo.AddAsync(record, ct);

            flag = false;
        }
        else if (record.QQChannels.Exists(q => q.ChannelId == cmd.QQChannel.Id))
        {
            flag = false;
        }

        (QQChannelSubscribeEntity channel, bool added) = await qsRepo.AddSubscribeAsync(
            cmd.QQChannel.GuildId,
            cmd.QQChannel.GuildName,
            cmd.QQChannel.Id,
            cmd.QQChannel.Name,
            record,
            ct);
        await configRepo.CreateOrUpdateAsync(channel, record, cmd.Configs, ct);

        await dbCtx.SaveChangesAsync(ct);

        return added && !flag
            ? $"成功：已添加订阅到 {cmd.QQChannel.Name} 子频道！"
            : $"成功：已更新在 {cmd.QQChannel.Name} 子频道中的此订阅的配置！";
    }

    public virtual async Task<string> Delete(SubscribeCommand cmd, CancellationToken ct = default)
    {
        if (cmd.SubscribeId == null)
            return $"请输入 {cmd.Platform} 对应的id！";

        try
        {
            TSubscribe subscribe = await repo.GetAsync(cmd.SubscribeId, true, ct);
            QQChannelSubscribeEntity record = await qsRepo.RemoveSubscribeAsync(cmd.QQChannel.Id, subscribe, ct);
            if (record.SubscribesAreAllEmpty())
                qsRepo.Delete(record);

            await repo.DeleteAsync(cmd.SubscribeId, ct);
            await configRepo.DeleteAsync(cmd.QQChannel.Id, cmd.SubscribeId, ct);

            await dbCtx.SaveChangesAsync(ct);
        }
        catch (InvalidOperationException iex)
        {
            Log.Error(iex, "The subscribe doesn't exist!\nInfo: {info}", new Dictionary<string, string>
            {
                { "type", typeof(TSubscribe).Name },
                { "subscribeId", cmd.SubscribeId },
                { "channelId", cmd.QQChannel.Id },
                { "channelName", cmd.QQChannel.Name }
            });
            return "错误：不存在该订阅！";
        }

        return $"成功：已删除在 {cmd.QQChannel.Name} 子频道中的此订阅！";
    }

    public virtual async Task<string> List(SubscribeCommand cmd, CancellationToken ct = default)
    {
        string subscribeType = typeof(TSubscribe).Name;
        string subscribeName = subscribeType.Replace("SubscribeEntity", "");
        subscribeType = subscribeType.Replace("Entity", "s");
        Dictionary<string, string> logInfo = new()
        {
            { "type", subscribeType },
            { "subscribeId", cmd.SubscribeId ?? "" },
            { "channelId", cmd.QQChannel.Id },
            { "channelName", cmd.QQChannel.Name }
        };

        if (cmd.Configs != null && cmd.Configs.TryGetValue("allChannels", out bool? allChannels) && allChannels is true)
            return await ListAllSubscribesInGuildAsync(cmd, ct);

        QQChannelSubscribeEntity? record = await qsRepo.GetOrDefaultAsync(
            cmd.QQChannel.Id, subscribeType, ct: ct);
        if (record == null)
        {
            Log.Warning("The channel subscribe hasn't any {subscribeType}.\nInfo: {info}", subscribeType, logInfo);
            return $"错误：{cmd.QQChannel.Name} 子频道未有 {subscribeName} 的任何订阅！";
        }

        List<TSubscribe>? subscribes = record.GetSubscribeProp<TSubscribe>();
        if (subscribes == null || subscribes.Count == 0)
        {
            Log.Warning("The channel subscribe hasn't any {subscirbeType}.\nInfo: {info}", subscribeType, logInfo);
            return $"错误：{cmd.QQChannel.Name} 子频道未有 {subscribeName} 的任何订阅！";
        }

        return cmd.SubscribeId == null
            ? await ListAllSubscribesInChannelAsync(
                cmd.QQChannel.Id, cmd.QQChannel.Name, subscribes, ct)
            : await ListSubscribeInChannelAsync(cmd, subscribes, logInfo, ct);
    }

    private async Task<string> ListAllSubscribesInGuildAsync(SubscribeCommand cmd, CancellationToken ct = default)
    {
        List<QQChannelSubscribeEntity> allChannels = await qsRepo.GetAllAsync(
            cmd.QQChannel.GuildId, ct: ct);
        if (allChannels.Count == 0)
            return "错误：当前频道的任何子频道都没有订阅！";

        List<Task<string>> tasks = [];
        foreach (QQChannelSubscribeEntity channel in allChannels)
        {
            List<TSubscribe>? subscribes = channel.GetSubscribeProp<TSubscribe>();
            if (subscribes == null || subscribes.Count == 0)
                continue;

            tasks.Add(ListAllSubscribesInChannelAsync(channel.ChannelId, channel.ChannelName, subscribes, ct));
        }

        if (tasks.Count == 0)
            return "错误：当前频道的任何子频道都没有订阅！";

        string[] result = await Task.WhenAll(tasks);
        return string.Join("\n\n", result);
    }

    private async Task<string> ListAllSubscribesInChannelAsync(
        string channelId,
        string channelName,
        List<TSubscribe> subscribes,
        CancellationToken ct = default)
    {
        string result = "";
        foreach (TSubscribe subscribe in subscribes)
        {
            result += "- " + subscribe.GetInfo("，");
            TConfig? config = await configRepo.GetOrDefaultAsync(channelId, subscribe.GetId(), ct: ct);
            if (config != null)
                result += "；配置：" + config.GetConfigs("，");
            result += "\n";
        }

        result = MyRegex().Replace(result, match => match.ToString().Replace(".", "*")); // 邮箱地址会被识别为链接导致无法过审

        return $"【子频道：{channelName}】\n" + result;
    }

    private async Task<string> ListSubscribeInChannelAsync(
        SubscribeCommand cmd,
        List<TSubscribe> subscribes,
        Dictionary<string, string> logInfo,
        CancellationToken ct = default)
    {
        (_, string? errCommandMsg) = await CheckId(cmd.SubscribeId!, ct);
        if (errCommandMsg != null)
            return errCommandMsg;

        TSubscribe? subscribe = subscribes.Find(s => s.GetId() == cmd.SubscribeId);
        if (subscribe == null)
        {
            Log.Warning(
                "The subscribe which id is {subscribeId} doesn't exist in the channel (id: {channelId}) subscribe!\nInfo: {info}",
                cmd.SubscribeId, cmd.QQChannel.Id, logInfo);
            return $"错误：id为 {cmd.SubscribeId} 的用户未在 {cmd.QQChannel.Name} 子频道订阅过！";
        }

        TConfig? config = await configRepo.GetOrDefaultAsync(
            cmd.QQChannel.Id, cmd.SubscribeId!, ct: ct);
        string result = config == null
            ? subscribe.GetInfo("，")
            : subscribe.GetInfo("，") + "；配置：" + config.GetConfigs("，") + "\n";

        result = MyRegex().Replace(result, match => match.ToString().Replace(".", "*"));

        return result;
    }

    [GeneratedRegex(@"[A-Za-z0-9-_\u4e00-\u9fa5]+@[a-zA-Z0-9_-]+(\.[a-zA-Z0-9_-]+)+")]
    private static partial Regex MyRegex();
}