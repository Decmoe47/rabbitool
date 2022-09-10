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
            if (!_isSandbox && message.ChannelId == _sandboxGuildId)
                return;
            Log.Information("Received an @ message.\nMessageId: {messageId}\nGuildId: {guildId}\nChannelId: {channelId}\nContent: {content}",
                message.Id, message.GuildId, message.ChannelId, message.Content);

            string text = await generateReplyMsgFunc(message, cancellationToken);
            try
            {
                await PostMessageAsync(
                    channelId: message.ChannelId,
                    text: text,
                    referenceMessageId: message.Id);
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

    public async Task<List<Guild>> GetAllGuildsAsync()
    {
        _limiter.Wait();
        return await _qqApi.GetUserApi().GetAllJoinedChannelsAsync();
    }

    public async Task<Guild> GetGuidAsync(string guildId)
    {
        List<Guild> guilds = await GetAllGuildsAsync();
        return guilds.First(c => c.Id == guildId);
    }

    public async Task<Guild> GetGuildByNameAsync(string name)
    {
        List<Guild> guilds = await GetAllGuildsAsync();
        return guilds.First(c => c.Name == name);
    }

    public async Task<Guild?> GetGuildByNameOrDefaultAsync(string name)
    {
        List<Guild> guilds = await GetAllGuildsAsync();
        return guilds.FirstOrDefault(c => c.Name == name);
    }

    public async Task<List<Channel>> GetAllChannelsAsync(string guildId)
    {
        _limiter.Wait();
        return await _qqApi.GetChannelApi().GetChannelsAsync(guildId);
    }

    public async Task<Channel> GetChannelAsync(string channelId)
    {
        _limiter.Wait();
        return await _qqApi.GetChannelApi().GetInfoAsync(channelId);
    }

    public async Task<Channel> GetChannelByNameAsync(string name, string guildId)
    {
        List<Channel> channels = await GetAllChannelsAsync(guildId);
        return channels.First(c => c.Name == name);
    }

    public async Task<Channel?> GetChannelByNameOrDefaultAsync(string name, string guildId)
    {
        List<Channel> channels = await GetAllChannelsAsync(guildId);
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
        string passiveReference = "")
    {
        if (!_isSandbox && channelId == _sandboxGuildId)
            return null;

        _limiter.Wait();

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

    public async Task<Message?> PushCommonMsgAsync(string channelId, string text)
    {
        return await PostMessageAsync(channelId, text);
    }

    public async Task<Message?> PushCommonMsgAsync(string channelId, string text, string imgUrl)
    {
        return await PostMessageAsync(channelId, text, imgUrl);
    }

    public async Task<Message?> PushCommonMsgAsync(string channelId, string text, List<string>? imgUrls)
    {
        if (imgUrls is null)
            return await PostMessageAsync(channelId, text);

        switch (imgUrls.Count)
        {
            case 0:
                return await PostMessageAsync(channelId, text);

            case 1:
                return await PostMessageAsync(channelId, text, imgUrls[0]);

            default:
                Message? msg = await PostMessageAsync(channelId, text, imgUrls[0]);
                List<Task<Message?>> tasks = new();
                foreach (string imgUrl in imgUrls.GetRange(1, imgUrls.Count - 1))
                    tasks.Add(PostMessageAsync(channelId, imgUrl: imgUrl));
                await Task.WhenAll(tasks);
                return msg;
        }
    }

    public async Task<(string, DateTime)?> PostThreadAsync(string channelId, string title, string text)
    {
        if (!_isSandbox && channelId == _sandboxGuildId)
            return null;

        _limiter.Wait();
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

    /// <summary>
    /// TODO: 初步实现，待优化
    /// </summary>
    public static List<Paragraph> TextToParagraphs(string text)
    {
        string splitPreceding, linePreceding, url;
        List<Paragraph> result = new();

        while (text.Contains('\n'))
        {
            List<Elem> precedingElems = new();
            (linePreceding, text) = SplitTextByLF(text);

            while (CommonUtil.ExistUrl(text))
            {
                (splitPreceding, text, url) = ExtractUrl(text);
                if (url == "")
                {
                    precedingElems.Add(new Elem()
                    {
                        Text = new TextElem() { Text = splitPreceding },
                        Type = ElemTypeEnum.Text,
                    });
                }
                else
                {
                    precedingElems.Add(new Elem()
                    {
                        Text = new TextElem() { Text = splitPreceding },
                        Type = ElemTypeEnum.Text,
                    });
                    precedingElems.Add(new Elem()
                    {
                        Url = new UrlElem() { Url = url, Desc = url },
                        Type = ElemTypeEnum.Url,
                    });
                }
            }

            precedingElems.Add(new Elem()
            {
                Text = new TextElem() { Text = linePreceding },
                Type = ElemTypeEnum.Text,
            });
            result.Add(new Paragraph()
            {
                Elems = precedingElems,
                Props = new ParagraphProps() { Alignment = AlignmentEnum.Left },
            });
        }

        (splitPreceding, string splitRest, url) = ExtractUrl(text);
        if (url == "")
        {
            result.Add(new Paragraph()
            {
                Elems = new List<Elem>()
                {
                    new Elem()
                    {
                        Text = new TextElem() { Text = text },
                        Type = ElemTypeEnum.Text,
                    }
                },
                Props = new ParagraphProps() { Alignment = AlignmentEnum.Left },
            });
        }
        else
        {
            result.Add(new Paragraph()
            {
                Elems = new List<Elem>()
                {
                    new Elem()
                    {
                        Text = new TextElem() { Text = splitPreceding },
                        Type = ElemTypeEnum.Text,
                    },
                    new Elem()
                    {
                        Url = new UrlElem() { Url = url, Desc = url },
                        Type = ElemTypeEnum.Url,
                    },
                    new Elem()
                    {
                        Text = new TextElem() { Text = splitRest },
                        Type = ElemTypeEnum.Text,
                    }
                },
                Props = new ParagraphProps() { Alignment = AlignmentEnum.Left },
            });
        }

        for (int i = 0; i < result.Count; i++)
        {
            if (result[i].Elems is List<Elem> elems)
            {
                foreach (Elem elem in elems)
                {
                    if (elem.Text?.Text is "")
                    {
                        result[i] = new Paragraph()
                        {
                            Props = new ParagraphProps() { Alignment = AlignmentEnum.Left }
                        };
                    }
                }
            }
        }

        return result;
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
                Elems = new List<Elem>()
                {
                    new Elem()
                    {
                        Text = new TextElem() { Text = "图片：" },
                        Type = ElemTypeEnum.Text
                    }
                },
                Props = new ParagraphProps() { Alignment = AlignmentEnum.Left }
            },
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
