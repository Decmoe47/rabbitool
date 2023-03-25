using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using QQChannelFramework.Api;
using QQChannelFramework.Exceptions;
using QQChannelFramework.Expansions.Bot;
using QQChannelFramework.Models;
using QQChannelFramework.Models.Forum.Contents;
using QQChannelFramework.Models.MessageModels;
using QQChannelFramework.Models.WsModels;
using Rabbitool.Common.Util;
using Rabbitool.Model.DTO.QQBot;
using Serilog;
using Channel = QQChannelFramework.Models.Channel;

namespace Rabbitool.Service;

public class QQBotService
{
    public bool IsOnline = false;

    private readonly QQChannelApi _qqApi;
    private readonly ChannelBot _qqBot;
    private readonly LimiterUtil _limiter;
    private readonly bool _isSandbox;
    private readonly string _sandboxGuildName;
    private string _sandboxGuildId = "";
    private string _botId = "";

    public QQBotService(string appId, string token, bool isSandbox, string sandboxGuildName)
    {
        _limiter = LimiterCollection.QQBotLimiter;
        _isSandbox = isSandbox;
        _sandboxGuildName = sandboxGuildName;

        OpenApiAccessInfo openApiAccessInfo = new()
        {
            BotAppId = appId,
            BotToken = token,
            BotSecret = ""
        };
        _qqApi = new(openApiAccessInfo);
        _qqApi.UseBotIdentity();
        if (isSandbox)
            _qqApi.UseSandBoxMode();
        _qqBot = new ChannelBot(_qqApi);
    }

    public async Task RunAsync()
    {
        RegisterBasicEvents();
        RegisterMessageAuditEvent();

        await _qqBot.OnlineAsync();
        IsOnline = true;

        Guild guild = await GetGuildByNameAsync(_sandboxGuildName);
        _sandboxGuildId = guild.Id;

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
            if (!_isSandbox && message.GuildId == _sandboxGuildId)
                return;
            if (!message.Content.Contains("<@!" + _botId + ">"))
                return;
            Log.Information("Received an @ message.\nMessageId: {messageId}\nGuildId: {guildId}\nChannelId: {channelId}\nContent: {content}",
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
                Log.Error(ex, ex.Message);
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
            => Log.Information("Message audit passed.\nAuditInfo: {auditInfo}", JsonConvert.SerializeObject(audit));
        _qqBot.MessageAuditReject += (audit)
            => Log.Error("Message audit rejected!\nAuditInfo: {auditInfo}", JsonConvert.SerializeObject(audit));
    }

    private void RegisterBasicEvents()
    {
        _qqBot.OnConnected += () =>
        {
            IsOnline = true;
            Log.Information("QQBot connected!");
        };
        _qqBot.OnError += async (ex) =>
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
            Log.Warning("QQBot connect closed!");
        };
    }

    private async Task<string> GetBotIdAsync()
    {
        User bot = await _qqApi.GetUserApi().GetCurrentUserAsync();
        return bot.Id;
    }

    public async Task<List<Guild>> GetAllGuildsAsync(CancellationToken ct = default)
    {
        _limiter.Wait(ct: ct);
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
        _limiter.Wait(ct: ct);
        return await _qqApi.GetChannelApi().GetChannelsAsync(guildId);
    }

    public async Task<Channel> GetChannelAsync(string channelId, CancellationToken ct = default)
    {
        _limiter.Wait(ct: ct);
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
        string? imgUrl = null,
        JObject? embed = null,
        JObject? ark = null,
        string? referenceMessageId = null,
        string passiveReference = "",
        CancellationToken ct = default)
    {
        if (!_isSandbox && channelId == _sandboxGuildId)
            return null;

        _limiter.Wait(ct: ct);

        try
        {
            Log.Information("Posting QQ channel message...\nChannelName: {channelName}\nImgUrl: {imgUrl}\nReferenceMessageId: {referenceMessageId}\nPassiveReference: {passiveReference}\nText: {text}",
                channelName, imgUrl ?? "", referenceMessageId ?? "", passiveReference, text ?? "");
            return await _qqApi
                .GetMessageApi()
                .SendMessageAsync(
                    channelId: channelId,
                    content: text,
                    image: imgUrl,
                    embed: embed,
                    ark: ark,
                    referenceMessageId: referenceMessageId,
                    passiveReference: passiveReference);
        }
        catch (ErrorResultException ex)
        {
            if (ex.Message.Contains("(304023)"))
            {
                Log.Information(ex.Message);
                return null;
            }
            else
            {
                throw new QQBotApiException(
                    $"Post message failed!\nChannelName: {channelName}\nImgUrl: {imgUrl}\nReferenceMessageId: {referenceMessageId}\nPassiveReference: {passiveReference}\nText: {text}", ex);
            }
        }
    }

    public async Task<Message?> PushCommonMsgAsync(string channelId, string channelName, string text, CancellationToken ct = default)
    {
        return await PostMessageAsync(channelId, channelName, text, ct: ct);
    }

    public async Task<Message?> PushCommonMsgAsync(
        string channelId, string channelName, string text, string imgUrl, CancellationToken ct = default)
    {
        return await PostMessageAsync(channelId, channelName, text, imgUrl, ct: ct);
    }

    public async Task<Message?> PushCommonMsgAsync(
        string channelId, string channelName, string text, List<string>? imgUrls, CancellationToken ct = default)
    {
        if (imgUrls == null)
            return await PostMessageAsync(channelId, channelName, text, ct: ct);

        switch (imgUrls.Count)
        {
            case 0:
                return await PostMessageAsync(channelId, channelName, text, ct: ct);

            case 1:
                return await PostMessageAsync(channelId, channelName, text, imgUrls[0], ct: ct);

            default:
                Message? msg = await PostMessageAsync(channelId, channelName, text, imgUrls[0], ct: ct);
                List<Task<Message?>> tasks = new();
                foreach (string imgUrl in imgUrls.GetRange(1, imgUrls.Count - 1))
                    tasks.Add(PostMessageAsync(channelId, channelName, imgUrl: imgUrl, ct: ct));
                await Task.WhenAll(tasks);
                return msg;
        }
    }

    public async Task<(string, DateTime)?> PostThreadAsync(
        string channelId, string channelName, string title, string text, CancellationToken ct = default)
    {
        if (!_isSandbox && channelId == _sandboxGuildId)
            return null;

        _limiter.Wait(ct: ct);
        try
        {
            Log.Information("Posting QQ channel thread...\nChannelName: {channelName}\nTitle: {title}\nText: {text}",
                channelName, title, text);
            return await _qqApi
                .GetForumApi()
                .Publish(title, channelId, new ThreadRichTextContent() { Content = text });
        }
        catch (ErrorResultException ex)
        {
            if (ex.Message.Contains("(304023)"))
            {
                Log.Information(ex.Message);
                return null;
            }
            else
            {
                throw new QQBotApiException($"Post Thread Failed!\nChannelName: {channelName}\nTitle: {title}\nText: {text}", ex);
            }
        }
    }

    public static RichTextDTO TextToRichText(string text)
    {
        List<ParagraphDTO> paras = new();

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
                    paras[^1].Elems?.Add(new ElemDTO()
                    {
                        Url = new UrlElemDTO() { Url = url, Desc = url },
                        Type = ElemTypeEnum.Url
                    });
                }
                else
                {
                    paras.Add(new ParagraphDTO()
                    {
                        Elems = new List<ElemDTO>()
                        {
                            new ElemDTO()
                            {
                                Url = new UrlElemDTO() { Url = line, Desc = line },
                                Type = ElemTypeEnum.Url
                            }
                        },
                        Props = new ParagraphPropsDTO() { Alignment = AlignmentEnum.Left }
                    });
                }
            }
            else
            {
                paras.Add(new ParagraphDTO()
                {
                    Elems = new List<ElemDTO>()
                    {
                        new ElemDTO()
                        {
                            Text = new TextElemDTO() {Text = line },
                            Type = ElemTypeEnum.Text,
                        }
                    },
                    Props = new ParagraphPropsDTO() { Alignment = AlignmentEnum.Left }
                });
            }
        }

        // 空白行
        for (int i = 0; i < paras.Count; i++)
        {
            if (paras[i].Elems is List<ElemDTO> elems)
            {
                foreach (ElemDTO elem in elems)
                {
                    if (elem.Text?.Text == "")
                    {
                        paras[i] = new ParagraphDTO()
                        {
                            Props = new ParagraphPropsDTO() { Alignment = AlignmentEnum.Left }
                        };
                    }
                }
            }
        }

        return new RichTextDTO() { Paragraphs = paras };
    }

    public static async Task<List<ParagraphDTO>> ImagesToParagraphsAsync(
        List<string> urls, CosService cosSvc, CancellationToken ct = default)
    {
        List<ElemDTO> imgElems = new(urls.Count);

        foreach (string url in urls)
        {
            string uploadedUrl = await cosSvc.UploadImageAsync(url, ct);
            imgElems.Add(new ElemDTO()
            {
                Image = new ImageElemDTO() { ThirdUrl = uploadedUrl },
                Type = ElemTypeEnum.Image
            });
        }

        return new()
        {
            new ParagraphDTO()
            {
                Elems = imgElems,
                Props = new ParagraphPropsDTO() { Alignment = AlignmentEnum.Middle }
            }
        };
    }

    public static async Task<List<ParagraphDTO>> VideoToParagraphsAsync(
        string tweetUrl, DateTime pubTime, CosService cosSvc, CancellationToken ct = default)
    {
        string url = await cosSvc.UploadVideoAsync(tweetUrl, pubTime, ct);

        return new()
        {
            new ParagraphDTO()
            {
                Elems = new List<ElemDTO>()
                {
                    new ElemDTO()
                    {
                        Text = new TextElemDTO() { Text = $"视频：{url}" },
                        Type = ElemTypeEnum.Text,
                    }
                },
                Props = new ParagraphPropsDTO() { Alignment = AlignmentEnum.Left }
            },
            new ParagraphDTO()
            {
                Elems = new List<ElemDTO>()
                {
                    new ElemDTO()
                    {
                        Video = new VideoElemDTO() { ThirdUrl = url },
                        Type = ElemTypeEnum.Video
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
