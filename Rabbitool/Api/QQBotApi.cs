using System.Text.RegularExpressions;
using System.Threading.RateLimiting;
using Autofac.Annotation;
using MyBot.Api;
using MyBot.Datas;
using MyBot.Exceptions;
using MyBot.Expansions.Bot;
using MyBot.Models;
using MyBot.Models.Forum;
using MyBot.Models.Forum.Contents;
using MyBot.Models.MessageModels;
using MyBot.Models.Types;
using MyBot.Models.WsModels;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Rabbitool.Common.Configs;
using Rabbitool.Common.Util;
using Serilog;

namespace Rabbitool.Api;

[Component]
public partial class QQBotApi
{
    private readonly CommonConfig _commonConfig;
    private readonly CosApi _cosApi;

    private readonly RateLimiter _limiter = new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
    {
        QueueLimit = 1,
        ReplenishmentPeriod = TimeSpan.FromSeconds(1),
        TokenLimit = 5,
        TokensPerPeriod = 5
    }); // See https://bot.q.qq.com/wiki/develop/api/openapi/message/post_messages.html

    private readonly ChannelBot _qqBot;
    private readonly QQBotConfig _qqBotConfig;
    private readonly QQChannelApi _qqChannelApi;
    private string _botId = "";
    private string _sandboxGuildId = "";
    public bool IsOnline;

    public QQBotApi(CosApi cosApi, CommonConfig commonConfig, QQBotConfig qqBotConfig)
    {
        _cosApi = cosApi;
        _commonConfig = commonConfig;
        _qqBotConfig = qqBotConfig;

        OpenApiAccessInfo openApiAccessInfo = new()
        {
            BotQQ = _qqBotConfig.BotQQ,
            BotAppId = _qqBotConfig.AppId,
            BotToken = _qqBotConfig.Token,
            BotSecret = _qqBotConfig.Secret
        };
        _qqChannelApi = new QQChannelApi(openApiAccessInfo);
        _qqChannelApi.UseBotIdentity();
        if (_commonConfig.InTestEnvironment)
            _qqChannelApi.UseSandBoxMode();
        _qqBot = new ChannelBot(_qqChannelApi);
    }

    public async Task RunBotAsync()
    {
        RegisterBasicEvents();
        RegisterMessageAuditEvent();

        await _qqBot.OnlineAsync();
        IsOnline = true;

        _sandboxGuildId = (await GetGuildByNameAsync(_qqBotConfig.SandboxGuildName)).Id;
        _botId = await GetBotIdAsync();
    }

    public void RegisterAtMessageEvent(
        Func<Message, CancellationToken, Task<string>> fn,
        CancellationToken ct = default)
    {
        _qqBot.RegisterAtMessageEvent();
        _qqBot.ReceivedAtMessage += async message =>
        {
            // 在沙箱频道里@bot，正式环境里的bot不会响应
            if (!_commonConfig.InTestEnvironment && message.GuildId == _sandboxGuildId)
                return;
            if (!message.Content.Contains("<@!" + _botId + ">"))
                return;
            Log.Information(
                "Received an @ message.\nMessageId: {messageId}\nGuildId: {guildId}\nChannelId: {channelId}\nContent: {content}",
                message.Id, message.GuildId, message.ChannelId, message.Content);

            string text = await fn(message, ct);
            try
            {
                Channel channel = await GetChannelAsync(message.ChannelId, ct);
                await PostMessageAsync(
                    message.ChannelId,
                    channel.Name,
                    text,
                    referenceMessageId: message.Id,
                    ct: ct);
            }
            catch (Exception ex)
            {
                Log.Error(ex, ex.Message);
            }
        };
    }

    public void RegisterBotDeletedEvent(
        Func<WsGuild, CancellationToken, Task> handler,
        CancellationToken ct = default)
    {
        _qqBot.RegisterGuildsEvent();
        _qqBot.BotBeRemoved += async guild => await handler(guild, ct); // TODO: bot被删除
    }

    private void RegisterMessageAuditEvent()
    {
        _qqBot.RegisterAuditEvent();
        _qqBot.MessageAuditPass += audit
            => Log.Information("Message audit passed.\nAuditInfo: {auditInfo}", JsonConvert.SerializeObject(audit));
        _qqBot.MessageAuditReject += audit
            => Log.Error("Message audit rejected!\nAuditInfo: {auditInfo}", JsonConvert.SerializeObject(audit));
    }

    private void RegisterBasicEvents()
    {
        _qqBot.OnConnected += () =>
        {
            IsOnline = true;
            Log.Debug("QQBot connected!");
        };
        _qqBot.OnError += async ex =>
        {
            IsOnline = false;
            Log.Error(ex, "QQBot error: {message}", ex.Message);
            if (ex.Message.Contains("websocket link does not exist"))
            {
                await _qqBot.OfflineAsync();
                await _qqBot.OnlineAsync();
            }
        };
        _qqBot.OnClose += () =>
        {
            IsOnline = false;
            Log.Debug("QQBot connect closed!");
        };
    }

    private async Task<string> GetBotIdAsync()
    {
        User bot = await _qqChannelApi.GetUserApi().GetCurrentUserAsync();
        return bot.Id;
    }

    public async Task<List<Guild>> GetAllGuildsAsync(CancellationToken ct = default)
    {
        await _limiter.AcquireAsync(1, ct);
        return await _qqChannelApi.GetUserApi().GetAllJoinedChannelsAsync();
    }

    public async Task<Guild> GetGuidAsync(string guildId, CancellationToken ct = default)
    {
        List<Guild> guilds = await GetAllGuildsAsync(ct);
        return guilds.First(c => c.Id == guildId);
    }

    public async Task<Guild> GetGuildByNameAsync(string name, CancellationToken ct = default)
    {
        List<Guild> guilds = await GetAllGuildsAsync(ct);
        return guilds.First(c => c.Name == name);
    }

    public async Task<Guild?> GetGuildByNameOrDefaultAsync(string name, CancellationToken ct = default)
    {
        List<Guild> guilds = await GetAllGuildsAsync(ct);
        return guilds.FirstOrDefault(c => c.Name == name);
    }

    public async Task<List<Channel>> GetAllChannelsAsync(string guildId, CancellationToken ct = default)
    {
        await _limiter.AcquireAsync(1, ct);
        return await _qqChannelApi.GetChannelApi().GetChannelsAsync(guildId);
    }

    public async Task<Channel> GetChannelAsync(string channelId, CancellationToken ct = default)
    {
        await _limiter.AcquireAsync(1, ct);
        return await _qqChannelApi.GetChannelApi().GetInfoAsync(channelId);
    }

    public async Task<Channel> GetChannelByNameAsync(string name, string guildId, CancellationToken ct = default)
    {
        List<Channel> channels = await GetAllChannelsAsync(guildId, ct);
        return channels.First(c => c.Name == name);
    }

    public async Task<Channel?> GetChannelByNameOrDefaultAsync(
        string name, string guildId, CancellationToken ct = default)
    {
        List<Channel> channels = await GetAllChannelsAsync(guildId, ct);
        return channels.FirstOrDefault(c => c.Name == name);
    }

    public async Task<bool> ExistChannelAsync(string channelId)
    {
        try
        {
            await GetChannelAsync(channelId);
            return true;
        }
        catch (HttpApiException ex)
        {
            if (ex.InnerException is AccessInfoErrorException)
                return false;
            throw;
        }
    }

    public async Task<Message?> PostMessageAsync(
        string channelId,
        string channelName,
        string? text = null,
        string? imgUrl = null,
        JObject? embed = null,
        JObject? ark = null,
        string? referenceMessageId = null,
        string passiveMsgId = "",
        CancellationToken ct = default)
    {
        if (!_commonConfig.InTestEnvironment && channelId == _sandboxGuildId)
            return null;

        await _limiter.AcquireAsync(1, ct);

        if (imgUrl != null)
            imgUrl = await _cosApi.UploadImageAsync(imgUrl, ct);

        try
        {
            Log.Debug(
                "Posting QQ channel message...\nChannelName: {channelName}\nReferenceMessageId: {referenceMessageId}\nPassiveMsgId: {passiveMsgId}\nText: {text}",
                channelName, referenceMessageId ?? "", passiveMsgId, text ?? "");
            return await _qqChannelApi
                .GetMessageApi()
                .SendMessageAsync(
                    channelId,
                    text,
                    imgUrl,
                    embed: embed,
                    ark: ark,
                    referenceMessageId: referenceMessageId,
                    passiveMsgId: passiveMsgId);
        }
        catch (MessageAuditException ex)
        {
            if (ex.Message.Contains("push message is waiting for audit now"))
            {
                Log.Information("Message is pushed successfully and waiting for audit now. (authId: {authId})",
                    ex.AuditId);
                return null;
            }

            throw new QQBotApiException(
                $"Post message failed!\nChannelName: {channelName}\nImgUrl: {imgUrl}\nReferenceMessageId: {referenceMessageId}\nPassiveMsgId: {passiveMsgId}\nText: {text}",
                ex);
        }
        catch (ErrorResultException rex)
        {
            throw new QQBotApiException(
                $"Post message failed!\nChannelName: {channelName}\nImgUrl: {imgUrl}\nReferenceMessageId: {referenceMessageId}\nPassiveMsgId: {passiveMsgId}\nText: {text}",
                rex);
        }
    }

    public async Task<Message?> PushCommonMsgAsync(
        string channelId, string channelName, string text, string? imgUrl = null, CancellationToken ct = default)
    {
        return await PostMessageAsync(channelId, channelName, text, imgUrl, ct: ct);
    }

    public async Task<Message?> PushCommonMsgAsync(
        string channelId, string channelName, string text, List<string>? imgUrls, CancellationToken ct = default)
    {
        switch (imgUrls?.Count)
        {
            case null or 0:
                return await PostMessageAsync(channelId, channelName, text, ct: ct);

            case 1:
                return await PostMessageAsync(channelId, channelName, text, imgUrls[0], ct: ct);

            default:
                text += "\n\n（更多图片请查看原链接）";
                Message? msg = await PostMessageAsync(channelId, channelName, text, imgUrls[0], ct: ct);

                return msg;
        }
    }

    public async Task<Message?> PushMarkdownMsgAsync(
        string channelId,
        string channelName,
        MessageMarkdown markdown,
        List<string>? otherImgs = null,
        string? referenceMessageId = null,
        string? passiveMsgId = null,
        string? passiveEventId = null,
        CancellationToken ct = default)
    {
        if (!_commonConfig.InTestEnvironment && channelId == _sandboxGuildId)
            return null;

        await _limiter.AcquireAsync(1, ct);

        try
        {
            Log.Debug(
                "Posting QQ channel message...\nChannelName: {channelName}\nReferenceMessageId: {referenceMessageId}\nPassiveMsgId: {passiveMsgId}\nMarkdown: {markdown}",
                channelName, referenceMessageId ?? "", passiveMsgId, JsonConvert.SerializeObject(markdown));
            Message msg = await _qqChannelApi
                .GetMessageApi()
                .SendMessageAsync(
                    channelId,
                    markdown: markdown,
                    referenceMessageId: referenceMessageId,
                    passiveMsgId: passiveMsgId,
                    passiveEventId: passiveEventId);
            if (otherImgs is not { Count: > 0 })
                return msg;
            foreach (string imgUrl in otherImgs)
                await PostMessageAsync(channelId, channelName, imgUrl, ct: ct);
            return msg;
        }
        catch (MessageAuditException ex)
        {
            if (ex.Message.Contains("message is waiting for audit now"))
            {
                Log.Information("Message is pushed successfully and waiting for audit now. (authId: {authId})",
                    ex.AuditId);
                return null;
            }

            throw new QQBotApiException(
                $"Post message failed!\nChannelName: {channelName}\nReferenceMessageId: {referenceMessageId}\nPassiveMsgId: {passiveMsgId}\nMarkdown: {JsonConvert.SerializeObject(markdown)}",
                ex);
        }
    }

    public async Task<(string, DateTime)?> PostThreadAsync(
        string channelId, string channelName, string title, string text, CancellationToken ct = default)
    {
        if (!_commonConfig.InTestEnvironment && channelId == _sandboxGuildId)
            return null;

        await _limiter.AcquireAsync(1, ct);
        try
        {
            Log.Information("Posting QQ channel thread...\nChannelName: {channelName}\nTitle: {title}\nText: {text}",
                channelName, title, text);
            return await _qqChannelApi
                .GetForumApi()
                .Publish(title, channelId, new ThreadRichTextContent { Content = text });
        }
        catch (ErrorResultException ex)
        {
            if (ex.Message.Contains("(304023)"))
            {
                Log.Information(ex.Message);
                return null;
            }

            throw new QQBotApiException(
                $"Post Thread Failed!\nChannelName: {channelName}\nTitle: {title}\nText: {text}", ex);
        }
    }

    public static RichText TextToRichText(string text)
    {
        List<Paragraph> paras = new();

        text = text.Replace("\r", "");
        List<string> textList = text.Split("\n").ToList();
        List<string> newTextList = new();

        foreach (string line in textList)
        {
            (string preceding, string rest, string url) = ExtractUrl(line);
            newTextList.Add(preceding);
            if (url != "")
                newTextList.Add("@isURL#" + url);
            if (rest != "")
                newTextList.Add(rest);
        }

        foreach (string line in newTextList)
            if (CommonUtil.ExistUrl(line))
            {
                if (line.StartsWith("@isURL#"))
                {
                    string url = line.Replace("@isURL#", "");
                    paras[^1].Elems?.Add(new Elem
                    {
                        Url = new URLElem { Url = url, Desc = url },
                        Type = ElemType.ELEM_TYPE_URL
                    });
                }
                else
                {
                    paras.Add(new Paragraph
                    {
                        Elems =
                        [
                            new Elem
                            {
                                Url = new URLElem { Url = line, Desc = line },
                                Type = ElemType.ELEM_TYPE_URL
                            }
                        ],
                        Props = new ParagraphProps { Alignment = Alignment.ALIGNMENT_LEFT }
                    });
                }
            }
            else
            {
                paras.Add(new Paragraph
                {
                    Elems =
                    [
                        new Elem
                        {
                            Text = new TextElem { Text = line },
                            Type = ElemType.ELEM_TYPE_TEXT
                        }
                    ],
                    Props = new ParagraphProps { Alignment = Alignment.ALIGNMENT_LEFT }
                });
            }

        // 空白行
        for (int i = 0; i < paras.Count; i++)
            if (paras[i].Elems is { } elems)
                foreach (Elem elem in elems)
                    if (elem.Text?.Text == "")
                        paras[i] = new Paragraph
                        {
                            Props = new ParagraphProps { Alignment = Alignment.ALIGNMENT_LEFT }
                        };

        return new RichText { Paragraphs = paras };
    }

    public static async Task<List<Paragraph>> ImagesToParagraphsAsync(
        List<string> urls, CosApi cosSvc, CancellationToken ct = default)
    {
        List<Elem> imgElems = new(urls.Count);

        foreach (string url in urls)
        {
            string uploadedUrl = await cosSvc.UploadImageAsync(url, ct);
            imgElems.Add(new Elem
            {
                Image = new ImageElem { Url = uploadedUrl },
                Type = ElemType.ELEM_TYPE_IMAGE
            });
        }

        return
        [
            new Paragraph
            {
                Elems = imgElems,
                Props = new ParagraphProps { Alignment = Alignment.ALIGNMENT_MIDDLE }
            }
        ];
    }

    public static async Task<List<Paragraph>> VideoToParagraphsAsync(
        string tweetUrl, DateTime pubTime, CosApi cosSvc, CancellationToken ct = default)
    {
        string url = await cosSvc.UploadVideoAsync(tweetUrl, pubTime, ct);

        return
        [
            new Paragraph
            {
                Elems =
                [
                    new Elem
                    {
                        Text = new TextElem { Text = $"视频：{url}" },
                        Type = ElemType.ELEM_TYPE_TEXT
                    }
                ],
                Props = new ParagraphProps { Alignment = Alignment.ALIGNMENT_LEFT }
            },

            new Paragraph
            {
                Elems =
                [
                    new Elem
                    {
                        Video = new VideoElem { Url = url },
                        Type = ElemType.ELEM_TYPE_VIDEO
                    }
                ]
            }
        ];
    }

    /// <summary>
    ///     查找url，遇到第一个url后以此url为分界限切割<paramref name="text" />，
    ///     返回此url，以及不包含此url的<paramref name="text" />前半部分和后半部分。
    ///     <para></para>
    ///     无url时<c>preceding</c>返回<paramref name="text" />原样，<c>rest</c>和<c>url</c>返回<c>""</c>
    /// </summary>
    /// <param name="text"></param>
    /// <returns></returns>
    private static (string preceding, string rest, string url) ExtractUrl(string text)
    {
        Match matched = MyRegex().Match(text);
        int i = matched.Index;
        int length = matched.Length;

        return matched.Success
            ? (text[..i], text[(i + length)..], text[i..(i + length)])
            : (text, "", "");
    }

    [GeneratedRegex(@"(http|https)://[\w\-_]+(\.[\w\-_]+)+([\w\-.,@?^=%&:/~+#]*[\w\-@?^=%&/~+#])?")]
    private static partial Regex MyRegex();
}