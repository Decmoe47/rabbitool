using System.Text.RegularExpressions;
using System.Threading.RateLimiting;
using Flurl.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using QQChannelFramework.Api;
using QQChannelFramework.Exceptions;
using QQChannelFramework.Expansions.Bot;
using QQChannelFramework.Models;
using QQChannelFramework.Models.Forum;
using QQChannelFramework.Models.Forum.Contents;
using QQChannelFramework.Models.MessageModels;
using QQChannelFramework.Models.Types;
using QQChannelFramework.Models.WsModels;
using Rabbitool.Common.Tool;
using Rabbitool.Common.Util;
using Rabbitool.Conf;
using Serilog;
using Channel = QQChannelFramework.Models.Channel;

namespace Rabbitool.Service;

public class QQBotService
{
    public bool IsOnline = false;

    private readonly QQChannelApi _qqApi;
    private readonly ChannelBot _qqBot;
    private string _sandboxGuildId = "";
    private string _botId = "";

    private readonly ILogger _log = Log.Logger;

    private readonly RateLimiter _limiter = new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
    {
        QueueLimit = 1,
        ReplenishmentPeriod = TimeSpan.FromSeconds(1),
        TokenLimit = 5,
        TokensPerPeriod = 5,
    });     // See https://bot.q.qq.com/wiki/develop/api/openapi/message/post_messages.html

    public QQBotService()
    {
        if (Configs.R.QQBot.Logger is not null)
            _log = LogConfiger.New(fileName: "qqBot");

        OpenApiAccessInfo openApiAccessInfo = new()
        {
            BotAppId = Configs.R.QQBot.AppId,
            BotToken = Configs.R.QQBot.Token,
            BotSecret = ""
        };
        _qqApi = new(openApiAccessInfo);
        _qqApi.UseBotIdentity();
        if (Configs.R.InTestEnvironment)
            _qqApi.UseSandBoxMode();
        _qqBot = new ChannelBot(_qqApi);
    }

    public async Task RunAsync()
    {
        RegisterBasicEvents();
        RegisterMessageAuditEvent();

        await _qqBot.OnlineAsync();
        IsOnline = true;

        _sandboxGuildId = (await GetGuildByNameAsync(Configs.R.QQBot.SandboxGuildName)).Id;
        _botId = await GetBotIdAsync();
    }

    public void RegisterAtMessageEvent(
        Func<Message, CancellationToken, Task<string>> fn,
        CancellationToken ct = default)
    {
        _qqBot.RegisterAtMessageEvent();
        _qqBot.ReceivedAtMessage += async (message) =>
        {
            // 在沙箱频道里@bot，正式环境里的bot不会响应
            if (!Configs.R.InTestEnvironment && message.GuildId == _sandboxGuildId)
                return;
            if (!message.Content.Contains("<@!" + _botId + ">"))
                return;
            _log.Information("Received an @ message.\nMessageId: {messageId}\nGuildId: {guildId}\nChannelId: {channelId}\nContent: {content}",
                message.Id, message.GuildId, message.ChannelId, message.Content);

            string text = await fn(message, ct);
            try
            {
                Channel channel = await GetChannelAsync(message.ChannelId);
                await PostMessageAsync(
                    channelId: message.ChannelId,
                    channelName: channel.Name,
                    text: text,
                    referenceMessageId: message.Id,
                    ct: ct);
            }
            catch (Exception ex)
            {
                _log.Error(ex, ex.Message);
            }
        };
    }

    public void RegisterChannelMessageEvent(
        Func<Message, CancellationToken, Task<string>> fn,
        CancellationToken ct = default)
    {
        _qqBot.RegisterUserMessageEvent();
        _qqBot.ReceivedUserMessage += (message) =>
        {
            if (message.Content.Contains("<@" + _botId + ">"))
                return;
        };
    }

    public void RegisterBotDeletedEvent(
        Func<WsGuild, CancellationToken, Task> handler,
        CancellationToken ct = default)
    {
        _qqBot.RegisterGuildsEvent();
        _qqBot.BotBeRemoved += async (guild) => await handler(guild, ct); // TODO: bot被删除
    }

    private void RegisterMessageAuditEvent()
    {
        _qqBot.RegisterAuditEvent();
        _qqBot.MessageAuditPass += (audit)
            => _log.Information("Message audit passed.\nAuditInfo: {auditInfo}", JsonConvert.SerializeObject(audit));
        _qqBot.MessageAuditReject += (audit)
            => _log.Error("Message audit rejected!\nAuditInfo: {auditInfo}", JsonConvert.SerializeObject(audit));
    }

    private void RegisterBasicEvents()
    {
        _qqBot.OnConnected += () =>
        {
            IsOnline = true;
            _log.Information("QQBot connected!");
        };
        _qqBot.OnError += async (ex) =>
        {
            IsOnline = false;
            _log.Error(ex, "QQBot error: {message}", ex.Message);
            if (ex.Message.Contains("websocket link does not exist"))
            {
                await _qqBot.OfflineAsync();
                await _qqBot.OnlineAsync();
            }
        };
        _qqBot.OnClose += () =>
        {
            IsOnline = false;
            _log.Warning("QQBot connect closed!");
        };
    }

    private async Task<string> GetBotIdAsync()
    {
        User bot = await _qqApi.GetUserApi().GetCurrentUserAsync();
        return bot.Id;
    }

    public async Task<List<Guild>> GetAllGuildsAsync(CancellationToken ct = default)
    {
        await _limiter.AcquireAsync(1, ct);
        return await _qqApi.GetUserApi().GetAllJoinedChannelsAsync();
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
        return await _qqApi.GetChannelApi().GetChannelsAsync(guildId);
    }

    public async Task<Channel> GetChannelAsync(string channelId, CancellationToken ct = default)
    {
        await _limiter.AcquireAsync(1, ct);
        return await _qqApi.GetChannelApi().GetInfoAsync(channelId);
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
            Channel channel = await GetChannelAsync(channelId);
            return true;
        }
        catch (HttpApiException ex)
        {
            return ex.InnerException is AccessInfoErrorException ? false : throw ex;
        }
    }

    public async Task<Message?> PostMessageAsync(
        string channelId,
        string channelName,
        string? text = null,
        byte[]? imgFile = null,
        JObject? embed = null,
        JObject? ark = null,
        string? referenceMessageId = null,
        string passiveReference = "",
        CancellationToken ct = default)
    {
        if (!Configs.R.InTestEnvironment && channelId == _sandboxGuildId)
            return null;

        await _limiter.AcquireAsync(1, ct);

        try
        {
            _log.Information("Posting QQ channel message...\nChannelName: {channelName}\nReferenceMessageId: {referenceMessageId}\nPassiveReference: {passiveReference}\nText: {text}",
                channelName, referenceMessageId ?? "", passiveReference, text ?? "");
            return await _qqApi
                .GetMessageApi()
                .SendMessageAsync(
                    channelId: channelId,
                    content: text,
                    imageData: imgFile,
                    embed: embed,
                    ark: ark,
                    referenceMessageId: referenceMessageId,
                    passiveMsgId: passiveReference);
        }
        catch (MessageAuditException ex)
        {
            if (ex.Message.Contains("push message is waiting for audit now"))
            {
                _log.Information(ex.Message);
                return null;
            }
            else
            {
                throw new QQBotApiException(
                    $"Post message failed!\nChannelName: {channelName}\nImgUrl: {imgFile}\nReferenceMessageId: {referenceMessageId}\nPassiveReference: {passiveReference}\nText: {text}", ex);
            }
        }
    }

    public async Task<Message?> PushCommonMsgAsync(
        string channelId, string channelName, string text, string? imgUrl = null, CancellationToken ct = default)
    {
        if (imgUrl != null)
        {
            byte[] img = await imgUrl.GetBytesAsync();
            return await PostMessageAsync(channelId, channelName, text, img, ct: ct);
        }
        else
        {
            return await PostMessageAsync(channelId, channelName, text, ct: ct);
        }
    }

    public async Task<Message?> PushCommonMsgAsync(
        string channelId, string channelName, string text, List<string>? imgUrls, CancellationToken ct = default)
    {
        switch (imgUrls?.Count)
        {
            case null or 0:
                return await PostMessageAsync(channelId, channelName, text, ct: ct);

            case 1:
                byte[] img = await imgUrls[0].GetBytesAsync();
                return await PostMessageAsync(channelId, channelName, text, img, ct: ct);

            default:
                byte[] firstImg = await imgUrls[0].GetBytesAsync();
                Message? msg = await PostMessageAsync(channelId, channelName, text, firstImg, ct: ct);

                foreach (string imgUrl in imgUrls.GetRange(1, imgUrls.Count - 1))
                {
                    byte[] otherImg = await imgUrl.GetBytesAsync();
                    await PostMessageAsync(channelId, channelName, imgFile: otherImg, ct: ct);
                }

                return msg;
        }
    }

    public async Task<(string, DateTime)?> PostThreadAsync(
        string channelId, string channelName, string title, string text, CancellationToken ct = default)
    {
        if (!Configs.R.InTestEnvironment && channelId == _sandboxGuildId)
            return null;

        await _limiter.AcquireAsync(1, ct);
        try
        {
            _log.Information("Posting QQ channel thread...\nChannelName: {channelName}\nTitle: {title}\nText: {text}",
                channelName, title, text);
            return await _qqApi
                .GetForumApi()
                .Publish(title, channelId, new ThreadRichTextContent() { Content = text });
        }
        catch (ErrorResultException ex)
        {
            if (ex.Message.Contains("(304023)"))
            {
                _log.Information(ex.Message);
                return null;
            }
            else
            {
                throw new QQBotApiException($"Post Thread Failed!\nChannelName: {channelName}\nTitle: {title}\nText: {text}", ex);
            }
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
        {
            if (CommonUtil.ExistUrl(line))
            {
                if (line.StartsWith("@isURL#"))
                {
                    string url = line.Replace("@isURL#", "");
                    paras[^1].Elems?.Add(new Elem()
                    {
                        Url = new URLElem() { Url = url, Desc = url },
                        Type = ElemType.ELEM_TYPE_URL
                    });
                }
                else
                {
                    paras.Add(new Paragraph()
                    {
                        Elems = new List<Elem>()
                        {
                            new Elem()
                            {
                                Url = new URLElem() { Url = line, Desc = line },
                                Type = ElemType.ELEM_TYPE_URL
                            }
                        },
                        Props = new ParagraphProps() { Alignment = Alignment.ALIGNMENT_LEFT }
                    });
                }
            }
            else
            {
                paras.Add(new Paragraph()
                {
                    Elems = new List<Elem>()
                    {
                        new Elem()
                        {
                            Text = new TextElem() {Text = line },
                            Type = ElemType.ELEM_TYPE_TEXT,
                        }
                    },
                    Props = new ParagraphProps() { Alignment = Alignment.ALIGNMENT_LEFT }
                });
            }
        }

        // 空白行
        for (int i = 0; i < paras.Count; i++)
        {
            if (paras[i].Elems is List<Elem> elems)
            {
                foreach (Elem elem in elems)
                {
                    if (elem.Text?.Text == "")
                    {
                        paras[i] = new Paragraph()
                        {
                            Props = new ParagraphProps() { Alignment = Alignment.ALIGNMENT_LEFT }
                        };
                    }
                }
            }
        }

        return new RichText() { Paragraphs = paras };
    }

    public static async Task<List<Paragraph>> ImagesToParagraphsAsync(
        List<string> urls, CosService cosSvc, CancellationToken ct = default)
    {
        List<Elem> imgElems = new(urls.Count);

        foreach (string url in urls)
        {
            string uploadedUrl = await cosSvc.UploadImageAsync(url, ct);
            imgElems.Add(new Elem()
            {
                Image = new ImageElem() { Url = uploadedUrl },
                Type = ElemType.ELEM_TYPE_IMAGE
            });
        }

        return new()
        {
            new Paragraph()
            {
                Elems = imgElems,
                Props = new ParagraphProps() { Alignment = Alignment.ALIGNMENT_MIDDLE }
            }
        };
    }

    public static async Task<List<Paragraph>> VideoToParagraphsAsync(
        string tweetUrl, DateTime pubTime, CosService cosSvc, CancellationToken ct = default)
    {
        string url = await cosSvc.UploadVideoAsync(tweetUrl, pubTime, ct);

        return new()
        {
            new Paragraph()
            {
                Elems = new List<Elem>()
                {
                    new Elem()
                    {
                        Text = new TextElem() { Text = $"视频：{url}" },
                        Type = ElemType.ELEM_TYPE_TEXT,
                    }
                },
                Props = new ParagraphProps() { Alignment = Alignment.ALIGNMENT_LEFT }
            },
            new Paragraph()
            {
                Elems = new List<Elem>()
                {
                    new Elem()
                    {
                        Video = new VideoElem() { Url = url },
                        Type = ElemType.ELEM_TYPE_VIDEO
                    }
                }
            }
        };
    }

    /// <summary>
    /// 查找url，遇到第一个url后以此url为分界限切割<paramref name="text"/>，
    /// 返回此url，以及不包含此url的<paramref name="text"/>前半部分和后半部分。
    /// <para></para>
    /// 无url时<c>preceding</c>返回<paramref name="text"/>原样，<c>rest</c>和<c>url</c>返回<c>""</c>
    /// </summary>
    /// <param name="text"></param>
    /// <returns></returns>
    private static (string preceding, string rest, string url) ExtractUrl(string text)
    {
        Match matched = Regex.Match(text, @"(http|https)://[\w\-_]+(\.[\w\-_]+)+([\w\-.,@?^=%&:/~+#]*[\w\-@?^=%&/~+#])?");
        int i = matched.Index;
        int length = matched.Length;

        return matched.Success
            ? (text[0..i], text[(i + length)..^0], text[i..(i + length)])
            : (text, "", "");
    }
}