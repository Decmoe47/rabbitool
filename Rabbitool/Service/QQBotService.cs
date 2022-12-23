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
    private readonly QQChannelApi _qqApi;
    private readonly ChannelBot _qqBot;
    private readonly LimiterUtil _limiter;
    private readonly bool _isSandbox;
    private readonly string _sandboxGuildName;
    private string _sandboxGuildId = "";

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

        Guild guild = await GetGuildByNameAsync(_sandboxGuildName);
        _sandboxGuildId = guild.Id;

        await _qqBot.OnlineAsync();
    }

    public void RegisterAtMessageEvent(
        Func<Message, CancellationToken, Task<string>> generateReplyMsgFunc,
        CancellationToken cancellationToken = default)
    {
        _qqBot.RegisterAtMessageEvent();
        _qqBot.ReceivedAtMessage += async (message) =>
        {
            if (!_isSandbox && message.GuildId == _sandboxGuildId)
                return;
            if (!message.Content.Contains("<@"))
                return;
            Log.Information("Received an @ message.\nMessageId: {messageId}\nGuildId: {guildId}\nChannelId: {channelId}\nContent: {content}",
                message.Id, message.GuildId, message.ChannelId, message.Content);

            string text = await generateReplyMsgFunc(message, cancellationToken);
            try
            {
                await PostMessageAsync(
                    channelId: message.ChannelId,
                    text: text,
                    referenceMessageId: message.Id,
                    cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                Log.Error(ex, ex.Message);
            }
        };
    }

    public void RegisterBotDeletedEvent(
        Func<WsGuild, CancellationToken, Task> handler,
        CancellationToken cancellationToken = default)
    {
        _qqBot.RegisterGuildsEvent();
        _qqBot.BotBeRemoved += async (guild) => await handler(guild, cancellationToken); // TODO: bot被删除
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
        _qqBot.OnConnected += () => Log.Information("QQBot connected!");
        _qqBot.OnError += (ex) => Log.Error(ex, "QQBot error: {message}", ex.Message);
        _qqBot.OnClose += () => Log.Warning("QQBot connect closed!");
    }

    public async Task<List<Guild>> GetAllGuildsAsync(CancellationToken cancellationToken = default)
    {
        _limiter.Wait(cancellationToken: cancellationToken);
        return await _qqApi.GetUserApi().GetAllJoinedChannelsAsync();
    }

    public async Task<Guild> GetGuidAsync(string guildId, CancellationToken cancellationToken = default)
    {
        List<Guild> guilds = await GetAllGuildsAsync(cancellationToken);
        return guilds.First(c => c.Id == guildId);
    }

    public async Task<Guild> GetGuildByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        List<Guild> guilds = await GetAllGuildsAsync(cancellationToken);
        return guilds.First(c => c.Name == name);
    }

    public async Task<Guild?> GetGuildByNameOrDefaultAsync(string name, CancellationToken cancellationToken = default)
    {
        List<Guild> guilds = await GetAllGuildsAsync(cancellationToken);
        return guilds.FirstOrDefault(c => c.Name == name);
    }

    public async Task<List<Channel>> GetAllChannelsAsync(string guildId, CancellationToken cancellationToken = default)
    {
        _limiter.Wait(cancellationToken: cancellationToken);
        return await _qqApi.GetChannelApi().GetChannelsAsync(guildId);
    }

    public async Task<Channel> GetChannelAsync(string channelId, CancellationToken cancellationToken = default)
    {
        _limiter.Wait(cancellationToken: cancellationToken);
        return await _qqApi.GetChannelApi().GetInfoAsync(channelId);
    }

    public async Task<Channel> GetChannelByNameAsync(string name, string guildId, CancellationToken cancellationToken = default)
    {
        List<Channel> channels = await GetAllChannelsAsync(guildId, cancellationToken);
        return channels.First(c => c.Name == name);
    }

    public async Task<Channel?> GetChannelByNameOrDefaultAsync(
        string name, string guildId, CancellationToken cancellationToken = default)
    {
        List<Channel> channels = await GetAllChannelsAsync(guildId, cancellationToken);
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
        string? text = null,
        string? imgUrl = null,
        JObject? embed = null,
        JObject? ark = null,
        string? referenceMessageId = null,
        string passiveReference = "",
        CancellationToken cancellationToken = default)
    {
        if (!_isSandbox && channelId == _sandboxGuildId)
            return null;

        _limiter.Wait(cancellationToken: cancellationToken);

        try
        {
            Log.Information("Posting QQ channel message...\nChannelId: {channelId}\nImgUrl: {imgUrl}\nReferenceMessageId: {referenceMessageId}\nPassiveReference: {passiveReference}\nText: {text}",
                channelId, imgUrl ?? "", referenceMessageId ?? "", passiveReference, text ?? "");
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
                    $"Post message failed!\nChannelId: {channelId}\nImgUrl: {imgUrl}\nReferenceMessageId: {referenceMessageId}\nPassiveReference: {passiveReference}\nText: {text}", ex);
            }
        }
    }

    public async Task<Message?> PushCommonMsgAsync(string channelId, string text, CancellationToken cancellationToken = default)
    {
        return await PostMessageAsync(channelId, text, cancellationToken: cancellationToken);
    }

    public async Task<Message?> PushCommonMsgAsync(
        string channelId, string text, string imgUrl, CancellationToken cancellationToken = default)
    {
        return await PostMessageAsync(channelId, text, imgUrl, cancellationToken: cancellationToken);
    }

    public async Task<Message?> PushCommonMsgAsync(
        string channelId, string text, List<string>? imgUrls, CancellationToken cancellationToken = default)
    {
        if (imgUrls is null)
            return await PostMessageAsync(channelId, text, cancellationToken: cancellationToken);

        switch (imgUrls.Count)
        {
            case 0:
                return await PostMessageAsync(channelId, text, cancellationToken: cancellationToken);

            case 1:
                return await PostMessageAsync(channelId, text, imgUrls[0], cancellationToken: cancellationToken);

            default:
                Message? msg = await PostMessageAsync(channelId, text, imgUrls[0], cancellationToken: cancellationToken);
                List<Task<Message?>> tasks = new();
                foreach (string imgUrl in imgUrls.GetRange(1, imgUrls.Count - 1))
                    tasks.Add(PostMessageAsync(channelId, imgUrl: imgUrl, cancellationToken: cancellationToken));
                await Task.WhenAll(tasks);
                return msg;
        }
    }

    public async Task<(string, DateTime)?> PostThreadAsync(
        string channelId, string title, string text, CancellationToken cancellationToken = default)
    {
        if (!_isSandbox && channelId == _sandboxGuildId)
            return null;

        _limiter.Wait(cancellationToken: cancellationToken);
        try
        {
            Log.Information("Posting QQ channel thread...\nChannelId: {channelId}\nTitle: {title}\nText: {text}",
                channelId, title, text);
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
                throw new QQBotApiException($"Post Thread Failed!\nChannelId: {channelId}\nTitle: {title}\nText: {text}", ex);
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
                        Url = new UrlElem() { Url = url, Desc = url },
                        Type = ElemTypeEnum.Url
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
                                Url = new UrlElem() { Url = line, Desc = line },
                                Type = ElemTypeEnum.Url
                            }
                        },
                        Props = new ParagraphProps() { Alignment = AlignmentEnum.Left }
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
                            Type = ElemTypeEnum.Text,
                        }
                    },
                    Props = new ParagraphProps() { Alignment = AlignmentEnum.Left }
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
                    if (elem.Text?.Text is "")
                    {
                        paras[i] = new Paragraph()
                        {
                            Props = new ParagraphProps() { Alignment = AlignmentEnum.Left }
                        };
                    }
                }
            }
        }

        return new RichText() { Paragraphs = paras };
    }

    public static async Task<List<Paragraph>> ImgagesToParagraphsAsync(
        List<string> urls, CosService cosSvc, CancellationToken cancellationToken = default)
    {
        List<Elem> imgElems = new(urls.Count);

        foreach (string url in urls)
        {
            string uploadedUrl = await cosSvc.UploadImageAsync(url, cancellationToken);
            imgElems.Add(new Elem()
            {
                Image = new ImageElem() { ThirdUrl = uploadedUrl },
                Type = ElemTypeEnum.Image
            });
        }

        return new()
        {
            new Paragraph()
            {
                Elems = imgElems,
                Props = new ParagraphProps() { Alignment = AlignmentEnum.Middle }
            }
        };
    }

    public static async Task<List<Paragraph>> VideoToParagraphsAsync(
        string tweetUrl, DateTime pubTime, CosService cosSvc, CancellationToken cancellationToken = default)
    {
        string url = await cosSvc.UploadVideoAsync(tweetUrl, pubTime, cancellationToken);

        return new()
        {
            new Paragraph()
            {
                Elems = new List<Elem>()
                {
                    new Elem()
                    {
                        Text = new TextElem() { Text = $"视频：{url}" },
                        Type = ElemTypeEnum.Text,
                    }
                },
                Props = new ParagraphProps() { Alignment = AlignmentEnum.Left }
            },
            new Paragraph()
            {
                Elems = new List<Elem>()
                {
                    new Elem()
                    {
                        Video = new VideoElem() { ThirdUrl = url },
                        Type = ElemTypeEnum.Video
                    }
                }
            }
        };
    }

    private static (string preceding, string rest) SplitTextByLF(string text)
    {
        int i = text.IndexOf('\n');
        return i == -1 ? (text, "") : (text[0..i], text[(i + 1)..^0]);
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
