using QQChannelFramework.Models;
using QQChannelFramework.Models.MessageModels;
using Rabbitool.Model.DTO.Command;
using Rabbitool.Repository.Subscribe;
using Rabbitool.Service;
using Serilog;

namespace Rabbitool.Plugin.Command.Subscribe;

public static class SubscribeCommandResponder
{
    public static readonly string[] SupportedPlatforms = new string[4] { "b站", "推特", "油管", "邮箱" };
    private static QQBotService _qbSvc = null!;
    private static string _userAgent = null!;
    private static string _dbPath = null!;

    public static readonly List<Rabbitool.Model.DTO.Command.CommandInfo> AllSubscribeCommands = new()
    {
        new Model.DTO.Command.CommandInfo
        {
            Name = "/订阅",
            Format = new string[4] { "/订阅", "[订阅平台]", "[订阅对象的id]", "[配置(可选)]" },
            Example = "/订阅 b站 434565011 pureForwardDynamicPush=true livePush=false",
            Responder = RespondToAddOrUpdateSubscribeCommandAsync
        },
        new Model.DTO.Command.CommandInfo
        {
            Name = "/取消订阅",
            Format = new string[4] { "/取消订阅", "[订阅平台]", "[订阅对象的id]", "[请求参数(可选)]" },
            Example = "/取消订阅 b站 434565011 bot通知",
            Responder = RespondToDeleteSubscribeCommandAsync
        },
        new Model.DTO.Command.CommandInfo
        {
            Name = "/列出订阅",
            Format = new string[4] { "/列出订阅", "[订阅平台]", "[订阅对象的id(可选，留空为所有)]", "[请求参数(可选)]" },
            Example = "/列出订阅 b站 434565011",
            Responder = RespondToListSubscribeCommandAsync
        }
    };

    public static void Init(QQBotService qbSvc, string dbPath, string userAgent)
    {
        _qbSvc = qbSvc;
        _dbPath = dbPath;
        _userAgent = userAgent;
    }

    public static async Task<string> RespondToAddOrUpdateSubscribeCommandAsync(
        List<string> cmd, Message msg, CancellationToken ct = default)
    {
        return await RespondToSubscribeCommandAsync(cmd, msg, SubscribeCommandType.AddOrUpdate, ct);
    }

    public static async Task<string> RespondToDeleteSubscribeCommandAsync(
        List<string> cmd, Message msg, CancellationToken ct = default)
    {
        return await RespondToSubscribeCommandAsync(cmd, msg, SubscribeCommandType.Delete, ct);
    }

    public static async Task<string> RespondToListSubscribeCommandAsync(
        List<string> cmd, Message msg, CancellationToken ct = default)
    {
        return await RespondToSubscribeCommandAsync(cmd, msg, SubscribeCommandType.List, ct);
    }

    private static async Task<string> RespondToSubscribeCommandAsync(
        List<string> cmd, Message msg, SubscribeCommandType cmdType, CancellationToken ct = default)
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

        List<string> configs = new();
        SubscribeConfigType configDict = new();
        string guildName = (await _qbSvc.GetGuidAsync(msg.GuildId, ct)).Name;
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
            Channel? channel = await _qbSvc.GetChannelByNameOrDefaultAsync(v, msg.GuildId, ct);
            if (channel is null)
                return $"错误：不存在名为 {v} 的子频道！";
            channelId = channel.Id;
            channelName = channel.Name;
        }
        else
        {
            Channel channel = await _qbSvc.GetChannelAsync(channelId, ct);
            channelName = channel.Name;
        }

        SubscribeCommandDTO subscribe = new()
        {
            Command = cmd[0],
            Platform = cmd[1],
            SubscribeId = subscribeId,
            QQChannel = new SubscribeCommandQQChannelDTO()
            {
                GuildName = guildName,
                GuildId = msg.GuildId,
                Id = channelId,
                Name = channelName
            },
            Configs = configDict.Count != 0 ? configDict : null
        };

        ISubscribeCommandHandler handler = GetSubscribeCommandHandler(subscribe.Platform, ct);

        switch (cmdType)
        {
            case SubscribeCommandType.AddOrUpdate:
                return await handler.Add(subscribe, ct);

            case SubscribeCommandType.Delete:
                return await handler.Delete(subscribe, ct);

            case SubscribeCommandType.List:
                return await handler.List(subscribe, ct);

            default:
                Log.Error("Not supported subscribe command type {type}", cmdType);
                return "错误：处理指令时发生内部错误！";
        }
    }

    private static dynamic ParseValue(string key, string value)
    {
        if (key == "channel")
            return value.Replace("\"", "");

        if (value == "true")
            return true;
        else if (value == "false")
            return false;
        else if (int.TryParse(value, out int intResult))
            return intResult;
        else if (float.TryParse(value, out float floatResult))
            return floatResult;
        else
            return value;
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="platform"></param>
    /// <returns></returns>
    /// <exception cref="UninitializedException"></exception>
    /// <exception cref="NotSupportedException"></exception>
    private static ISubscribeCommandHandler GetSubscribeCommandHandler(string platform, CancellationToken ct)
    {
        if (_qbSvc == null || _userAgent == null)
        {
            throw new UninitializedException(
                "You must initialize SubscribeCommandResponder first by SubscribeCommandResponder.setting()!");
        }

        SubscribeDbContext dbCtx = new(_dbPath);
        QQChannelSubscribeRepository qsRepo = new(dbCtx);

        ISubscribeCommandHandler handler;

        switch (platform)
        {
            case "b站":
                handler = new BilibiliSubscribeCommandHandler(
                    _qbSvc,
                    _userAgent,
                    dbCtx,
                    qsRepo,
                    new BilibiliSubscribeRepository(dbCtx),
                    new BilibiliSubscribeConfigRepository(dbCtx));
                break;

            case "推特":
                handler = new TwitterSubscribeCommandHandler(
                        _qbSvc,
                        _userAgent,
                        dbCtx,
                        qsRepo,
                        new TwitterSubscribeRepository(dbCtx),
                        new TwitterSubscribeConfigRepository(dbCtx));
                break;

            case "油管":
                handler = new YoutubeSubscribeCommandHandler(
                        _qbSvc,
                        _userAgent,
                        dbCtx,
                        qsRepo,
                        new YoutubeSubscribeRepository(dbCtx),
                        new YoutubeSubscribeConfigRepository(dbCtx));
                break;

            case "邮箱":
                handler = new MailSubscribeCommandHandler(
                        _qbSvc,
                        _userAgent,
                        dbCtx,
                        qsRepo,
                        new MailSubscribeRepository(dbCtx),
                        new MailSubscribeConfigRepository(dbCtx));
                break;

            default:
                throw new NotSupportedException($"Not supported platform {platform}");
        };

        _qbSvc.RegisterBotDeletedEvent(handler.BotDeletedHandlerAsync, ct);
        return handler;
    }
}

public enum SubscribeCommandType
{
    AddOrUpdate,
    Delete,
    List
}
