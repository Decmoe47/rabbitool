using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using QQChannelFramework.Api;
using QQChannelFramework.Exceptions;
using QQChannelFramework.Expansions.Bot;
using QQChannelFramework.Models;
using QQChannelFramework.Models.Forum.Contents;
using QQChannelFramework.Models.MessageModels;
using Rabbitool.Common.Util;
using Rabbitool.Model.DTO.QQBot;
using Serilog;

namespace Rabbitool.Service;

public class QQBotService
{
    private readonly QQChannelApi _qqApi;
    private readonly ChannelBot _qqBot;
    private readonly LimiterUtil _limiter;

    public QQBotService(string appId, string token, bool isSandbox)
    {
        _limiter = LimiterCollection.QQBotLimter;

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
        RegisterMessageAuditEvent();
        await _qqBot.OnlineAsync();
    }

    public void RegisterAtMessageEvent(
        Func<Message, CancellationToken, Task<string>> generateReplyMsgFunc,
        CancellationToken cancellationToken = default)
    {
        _qqBot.RegisterAtMessageEvent();
        _qqBot.ReceivedAtMessage += async (message) =>
        {
            string text = await generateReplyMsgFunc(message, cancellationToken);
            await PostMsgAsync(
                channelId: message.ChannelId,
                text: text,
                referenceMessageId: message.Id);
        };
    }

    private void RegisterMessageAuditEvent()
    {
        _qqBot.RegisterAuditEvent();
        _qqBot.MessageAuditPass += (audit)
            => Log.Information("Message audit passed.\nAuditInfo: {auditInfo}", JsonConvert.SerializeObject(audit));
        _qqBot.MessageAuditReject += (audit)
            => Log.Error("Message audit rejected!\nAuditInfo: {auditInfo}", JsonConvert.SerializeObject(audit));
    }

    public async Task<List<Channel>> GetAllChannelsAsync()
    {
        _limiter.Wait();
        List<Guild> guilds = await _qqApi.GetUserApi().GetAllJoinedChannelsAsync();
        List<Channel> channels = await _qqApi.GetChannelApi().GetChannelsAsync(guilds[0].Id);
        return channels;
    }

    public async Task<Channel> GetChannelAsync(string channelId)
    {
        _limiter.Wait();
        return await _qqApi.GetChannelApi().GetInfoAsync(channelId);
    }

    public async Task<Channel> GetChannelByNameAsync(string name)
    {
        List<Channel> channels = await GetAllChannelsAsync();
        return channels.First(c => c.Name == name);
    }

    public async Task<Channel?> GetChannelByNameOrDefaultAsync(string name)
    {
        List<Channel> channels = await GetAllChannelsAsync();
        return channels.FirstOrDefault(c => c.Name == name);
    }

    public async Task<bool> ExistChannelAsync(string channelId)
    {
        try
        {
            Channel channel = await GetChannelAsync(channelId);
            return true;
        }
        catch (ErrorResultException)
        {
            return false;
        }
    }

    public async Task<Message?> PostMsgAsync(
        string channelId,
        string? text = null,
        string? imgUrl = null,
        JObject? embed = null,
        JObject? ark = null,
        string? referenceMessageId = null,
        string passiveReference = "")
    {
        _limiter.Wait();

        try
        {
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
                throw ex;
            }
        }
    }

    public async Task<Message?> PushCommonMsgAsync(string channelId, string text)
    {
        return await PostMsgAsync(channelId, text);
    }

    public async Task<Message?> PushCommonMsgAsync(string channelId, string text, string imgUrl)
    {
        return await PostMsgAsync(channelId, text, imgUrl);
    }

    public async Task<Message?> PushCommonMsgAsync(string channelId, string text, List<string> imgUrls)
    {
        switch (imgUrls.Count)
        {
            case 0:
                return await PostMsgAsync(channelId, text);

            case 1:
                return await PostMsgAsync(channelId, text, imgUrls[0]);

            default:
                Message? msg = await PostMsgAsync(channelId, text, imgUrls[0]);
                var tasks = new List<Task<Message?>>();
                foreach (string imgUrl in imgUrls)
                    tasks.Add(PostMsgAsync(channelId, text, imgUrl));
                await Task.WhenAll(tasks);
                return msg;
        }
    }

    public async Task<(string, DateTime)?> PostThreadAsync(string channelID, string title, string text)
    {
        try
        {
            return await _qqApi
                .GetForumApi()
                .Publish(title, channelID, new ThreadRichTextContent() { Content = text });
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
                throw ex;
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

        return i == -1
            ? (text, "", "")
            : (text[0..i], text[length..^0], text[i..length]);
    }
}
