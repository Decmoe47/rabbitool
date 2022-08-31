using QQChannelFramework.Models;
using Rabbitool.Model.DTO.Command;
using Rabbitool.Repository.Subscribe;
using Rabbitool.Service;
using Serilog;

namespace Rabbitool.Plugin.Command.Subscribe;

public class SubscribeCommandResponder
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
        List<string> command, string channelId, CancellationToken cancellation = default)
    {
        return await RespondToSubscribeCommandAsync(command, channelId, SubscribeCommandType.AddOrUpdate, cancellation);
    }

    public static async Task<string> RespondToDeleteSubscribeCommandAsync(
        List<string> command, string channelId, CancellationToken cancellation = default)
    {
        return await RespondToSubscribeCommandAsync(command, channelId, SubscribeCommandType.Delete, cancellation);
    }

    public static async Task<string> RespondToListSubscribeCommandAsync(
        List<string> command, string channelId, CancellationToken cancellation = default)
    {
        return await RespondToSubscribeCommandAsync(command, channelId, SubscribeCommandType.List, cancellation);
    }

    private static async Task<string> RespondToSubscribeCommandAsync(
        List<string> command, string channelId, SubscribeCommandType commandType, CancellationToken cancellation = default)
    {
        if (commandType == SubscribeCommandType.List && command.Count < 2)
            return "错误：参数不足！";
        else if (command.Count < 3)
            return "错误：参数不足！";

        if (!SupportedPlatforms.Contains(command[1]))
            return $"错误：不支持的订阅平台！\n当前支持的订阅平台：{string.Join(" ", SupportedPlatforms)}";

        List<string> configs = command[2].Contains('=')
            ? command.GetRange(2, command.Count - 2)
            : command.GetRange(3, command.Count - 3);
        SubscribeConfigType configDict = new();
        string channelName;

        foreach (string config in configs)
        {
            string[] kv = config.Split('=');
            configDict.Add(kv[0], ParseValue(kv[0], kv[1]));
        }

        if (configDict.TryGetValue("channel", out dynamic? v) && v is string)
        {
            Channel? channel = await _qbSvc.GetChannelByNameOrDefaultAsync(v);
            if (channel is null)
                return $"错误：不存在名为 {v} 的子频道！";
            channelId = channel.Id;
            channelName = channel.Name;
        }
        else
        {
            Channel channel = await _qbSvc.GetChannelAsync(channelId);
            channelName = channel.Name;
        }

        SubscribeCommandDTO subscribe = new()
        {
            Command = command[0],
            Platform = command[1],
            SubscribeId = command.Count == 2 ? "" : command[2],
            QQChannel = new SubscribeCommandQQChannelDTO()
            {
                Id = channelId,
                Name = channelName
            },
            Configs = configDict
        };

        ISubscribeCommandHandler handler = GetSubscribeCommandHandler(subscribe.Platform);

        switch (commandType)
        {
            case SubscribeCommandType.AddOrUpdate:
                return await handler.Add(subscribe, cancellation);

            case SubscribeCommandType.Delete:
                return await handler.Delete(subscribe, cancellation);

            case SubscribeCommandType.List:
                return await handler.List(subscribe, cancellation);

            default:
                Log.Error("Not supported subscribe command type {type}", commandType);
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
    private static ISubscribeCommandHandler GetSubscribeCommandHandler(string platform)
    {
        if (_qbSvc == null || _userAgent == null)
        {
            throw new UninitializedException(
                "You must initialize SubscribeCommandResponder first by SubscribeCommandResponder.setting()!");
        }

        SubscribeDbContext dbCtx = new(_dbPath);
        QQChannelSubscribeRepository qsRepo = new(dbCtx);

        return platform switch
        {
            "b站" => new BilibiliSubscribeCommandHandler(
                    _qbSvc,
                    _userAgent,
                    dbCtx,
                    qsRepo,
                    new BilibiliSubscribeRepository(dbCtx),
                    new BilibiliSubscribeConfigRepository(dbCtx)),
            "推特" => new TwitterSubscribeCommandHandler(
                    _qbSvc,
                    _userAgent,
                    dbCtx,
                    qsRepo,
                    new TwitterSubscribeRepository(dbCtx),
                    new TwitterSubscribeConfigRepository(dbCtx)),
            "油管" => new YoutubeSubscribeCommandHandler(
                    _qbSvc,
                    _userAgent,
                    dbCtx,
                    qsRepo,
                    new YoutubeSubscribeRepository(dbCtx),
                    new YoutubeSubscribeConfigRepository(dbCtx)),
            "邮箱" => new MailSubscribeCommandHandler(
                    _qbSvc,
                    _userAgent,
                    dbCtx,
                    qsRepo,
                    new MailSubscribeRepository(dbCtx),
                    new MailSubscribeConfigRepository(dbCtx)),
            _ => throw new NotSupportedException($"Not supported platform {platform}"),
        };
    }
}

public enum SubscribeCommandType
{
    AddOrUpdate,
    Delete,
    List
}
