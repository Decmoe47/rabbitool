using System.Xml;
using System.Xml.XPath;
using CodeHollow.FeedReader;
using Flurl.Http;
using HtmlAgilityPack;
using Rabbitool.Common.Util;
using Rabbitool.Model.DTO.Youtube;

namespace Rabbitool.Service;

public class YoutubeService
{
    private readonly LimiterUtil _limiter = LimiterCollection.YoutubeLimter;

    public YoutubeService()
    {
    }

    public async Task<YoutubeBase> GetLatestVideoOrLiveAsync(string channelId, CancellationToken cancellationToken = default)
    {
        _limiter.Wait();

        Feed feed = await FeedReader.ReadAsync(
            $"https://www.youtube.com/feeds/videos.xml?channel_id={channelId}", cancellationToken);
        FeedItem item = feed.Items[0];
        string url = item.Link;

        XmlDocument doc = new();
        XmlNamespaceManager nsmgr = new(doc.NameTable);
        nsmgr.AddNamespace("yt", "http://www.youtube.com/xml/schemas/2015");
        nsmgr.AddNamespace("media", "http://search.yahoo.com/mrss/");

        return await IsLiveRoomAsync(channelId, url, cancellationToken)
            ? new YoutubeLive()
            {
                ChannelId = channelId,
                Type = YoutubeTypeEnum.Live,
                Author = item.Author,
                Id = item.SpecificItem.Element.XPathSelectElement(@"yt:videoId", nsmgr)!.Value,
                Title = item.Title,
                ThumbnailUrl = item.SpecificItem.Element
                    .XPathSelectElement(@"media:group/media:thumbnail", nsmgr)!
                    .Attribute("url")!.Value,
                Url = url,
                LiveStartTime = DateTime.Now.ToUniversalTime()
            }
            : new YoutubeVideo()
            {
                ChannelId = channelId,
                Type = YoutubeTypeEnum.Live,
                Author = item.Author,
                Id = item.SpecificItem.Element.XPathSelectElement(@"yt:videoId", nsmgr)!.Value,
                Title = item.Title,
                ThumbnailUrl = item.SpecificItem.Element
                    .XPathSelectElement(@"media:group/media:thumbnail", nsmgr)!
                    .Attribute("url")!.Value,
                Url = url,
                PubTime = (DateTime)item.PublishingDate!
            };
    }

    private async Task<bool> IsLiveRoomAsync(string channelId, string url, CancellationToken cancellationToken = default)
    {
        _limiter.Wait();

        string resp = await $"https://youtube.com/channel/{channelId}/live".GetStringAsync(cancellationToken);

        HtmlDocument doc = new();
        doc.LoadHtml(resp);

        return url == doc.DocumentNode.SelectSingleNode("//link[@rel=\"canonical\"]").Attributes["href"].Value;
    }
}
