using Autofac.Annotation;
using Microsoft.Extensions.DependencyInjection;
using MyBot.Models;
using MyBot.Models.MessageModels;
using Rabbitool.Api;
using Rabbitool.Common.Exception;
using Rabbitool.Common.Provider;
using Rabbitool.Model.DTO.Command;
using Rabbitool.Plugin.Command.Subscribe.Handler;
using Serilog;
using MyCommandInfo = Rabbitool.Model.DTO.Command.CommandInfo;

namespace Rabbitool.Plugin.Command.Subscribe;

[Component]
public class SubscribeCommands(
    QQBotApi qqBotApi,
    IServiceProvider serviceProvider,
    ICancellationTokenProvider ctp)
{
    private static readonly string[] SupportedPlatforms = ["b站", "推特", "油管", "邮箱"];

    public List<MyCommandInfo> GetAllCommands()
    {
        return
        [
            new MyCommandInfo
            {
                Name = "/订阅",
                Format = ["/订阅", "[订阅平台]", "[订阅对象的id]", "[配置(可选)]"],
                Example = "/订阅 b站 434565011 pureForwardDynamicPush=true livePush=false",
                Responder = RespondToAddOrUpdateSubscribeCommandAsync
            },
            new MyCommandInfo
            {
                Name = "/取消订阅",
                Format = ["/取消订阅", "[订阅平台]", "[订阅对象的id]", "[请求参数(可选)]"],
                Example = "/取消订阅 b站 434565011 bot通知",
                Responder = RespondToDeleteSubscribeCommandAsync
            },
            new MyCommandInfo
            {
                Name = "/列出订阅",
                Format = ["/列出订阅", "[订阅平台]", "[订阅对象的id(可选，留空为所有)]", "[请求参数(可选)]"],
                Example = "/列出订阅 b站 434565011",
                Responder = RespondToListSubscribeCommandAsync
            }
        ];
    }

    public async Task<string> RespondToAddOrUpdateSubscribeCommandAsync(List<string> cmd, Message msg)
    {
        return await RespondToSubscribeCommandAsync(cmd, msg, SubscribeCommandType.AddOrUpdate);
    }

    public async Task<string> RespondToDeleteSubscribeCommandAsync(List<string> cmd, Message msg)
    {
        return await RespondToSubscribeCommandAsync(cmd, msg, SubscribeCommandType.Delete);
    }

    public async Task<string> RespondToListSubscribeCommandAsync(List<string> cmd, Message msg)
    {
        return await RespondToSubscribeCommandAsync(cmd, msg, SubscribeCommandType.List);
    }

    private async Task<string> RespondToSubscribeCommandAsync(
        List<string> cmd, Message msg, SubscribeCommandType cmdType)
    {
        if (cmdType == SubscribeCommandType.List)
        {
            if (cmd.Count < 2)
                return "错误：参数不足！";
        }
        else if (cmd.Count < 3)
        {
            return "错误：参数不足！";
        }

        if (!SupportedPlatforms.Contains(cmd[1]))
            return $"错误：不支持的订阅平台！\n当前支持的订阅平台：{string.Join(" ", SupportedPlatforms)}";

        List<string> configs = [];
        SubscribeConfigType configDict = new();
        string guildName = (await qqBotApi.GetGuidAsync(msg.GuildId)).Name;
        string channelName;
        string channelId = msg.ChannelId;
        string? subscribeId = null;

        if (cmdType == SubscribeCommandType.List)
        {
            if (cmd.Count == 3 && cmd[2].Contains('='))
                configs = cmd.GetRange(2, cmd.Count - 2);
        }
        else
        {
            subscribeId = cmd[2];
            if (cmd.Count >= 4 && cmd[3].Contains('='))
                configs = cmd.GetRange(3, cmd.Count - 3);
        }

        foreach (string config in configs)
        {
            string[] kv = config.Split('=');
            if (kv[0] != "" && kv[1] != "")
                configDict.Add(kv[0], ParseValue(kv[0], kv[1]));
        }

        if (configDict.TryGetValue("channel", out dynamic? v) && v is string && cmdType != SubscribeCommandType.Delete)
        {
            Channel? channel = await qqBotApi.GetChannelByNameOrDefaultAsync(v, msg.GuildId);
            if (channel == null)
                return $"错误：不存在名为 {v} 的子频道！";
            channelId = channel.Id;
            channelName = channel.Name;
        }
        else
        {
            Channel channel = await qqBotApi.GetChannelAsync(channelId);
            channelName = channel.Name;
        }

        SubscribeCommand subscribe = new()
        {
            Command = cmd[0],
            Platform = cmd[1],
            SubscribeId = subscribeId,
            QQChannel = new SubscribeCommandQQChannel
            {
                GuildName = guildName,
                GuildId = msg.GuildId,
                Id = channelId,
                Name = channelName
            },
            Configs = configDict.Count != 0 ? configDict : null
        };

        ISubscribeCommandHandler handler = GetSubscribeCommandHandler(subscribe.Platform);

        switch (cmdType)
        {
            case SubscribeCommandType.AddOrUpdate:
                return await handler.Add(subscribe);

            case SubscribeCommandType.Delete:
                return await handler.Delete(subscribe);

            case SubscribeCommandType.List:
                return await handler.List(subscribe);

            default:
                Log.Error("Not supported subscribe command type {type}", cmdType);
                return "错误：处理指令时发生内部错误！";
        }
    }

    private static dynamic ParseValue(string key, string value)
    {
        if (key == "channel")
            return value.Replace("\"", "");

        switch (value)
        {
            case "true":
                return true;
            case "false":
                return false;
            default:
            {
                if (int.TryParse(value, out int intResult))
                    return intResult;
                if (float.TryParse(value, out float floatResult))
                    return floatResult;
                return value;
            }
        }
    }

    /// <summary>
    /// </summary>
    /// <param name="platform"></param>
    /// <returns></returns>
    /// <exception cref="UninitializedException"></exception>
    /// <exception cref="NotSupportedException"></exception>
    private ISubscribeCommandHandler GetSubscribeCommandHandler(string platform)
    {
        ISubscribeCommandHandler handler = platform switch
        {
            "b站" => serviceProvider.GetRequiredService<BilibiliSubscribeCommandHandler>(),
            "推特" => serviceProvider.GetRequiredService<TwitterSubscribeCommandHandler>(),
            "油管" => serviceProvider.GetRequiredService<YoutubeSubscribeCommandHandler>(),
            "邮箱" => serviceProvider.GetRequiredService<MailSubscribeCommandHandler>(),
            _ => throw new NotSupportedException($"Not supported platform {platform}")
        };
        qqBotApi.RegisterBotDeletedEvent(handler.BotDeletedHandlerAsync, ctp.Token);

        return handler;
    }
}

public enum SubscribeCommandType
{
    AddOrUpdate,
    Delete,
    List
}