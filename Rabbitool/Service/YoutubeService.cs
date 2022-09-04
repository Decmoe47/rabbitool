using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using Rabbitool.Common.Util;
using Rabbitool.Model.DTO.Youtube;

namespace Rabbitool.Service;

public class YoutubeService
{
    private readonly LimiterUtil _limiter = LimiterCollection.YoutubeLimter;
    private readonly YouTubeService _ytb;

    public YoutubeService(string apiKey)
    {
        _ytb = new YouTubeService(new BaseClientService.Initializer
        {
            ApplicationName = "Rabbitool",
            ApiKey = apiKey
        });
    }

    public async Task<YoutubeBase> GetLatestVideoOrLiveAsync(string channelId, CancellationToken cancellationToken = default)
    {
        _limiter.Wait();

        // https://developers.google.com/youtube/v3/docs/channels
        ChannelsResource.ListRequest channelsReq = _ytb.Channels
            .List(new Google.Apis.Util.Repeatable<string>(new string[3] { "snippet", "contentDetails", "statistics" }));
        channelsReq.Id = channelId;
        Channel channel = (await channelsReq.ExecuteAsync(cancellationToken)).Items[0];

        // https://developers.google.com/youtube/v3/docs/playlistItems
        PlaylistItemsResource.ListRequest playListReq = _ytb.PlaylistItems
            .List(new Google.Apis.Util.Repeatable<string>(new string[3] { "contentDetails", "snippet", "status" }));
        playListReq.PlaylistId = channel.ContentDetails.RelatedPlaylists.Uploads;
        PlaylistItem item = (await playListReq.ExecuteAsync(cancellationToken)).Items[0];

        // https://developers.google.com/youtube/v3/docs/videos
        VideosResource.ListRequest videosReq = _ytb.Videos
            .List(new Google.Apis.Util.Repeatable<string>(new string[4] { "snippet", "contentDetails", "statistics", "liveStreamingDetails" }));
        videosReq.Id = item.ContentDetails.VideoId;
        Video video = (await videosReq.ExecuteAsync(cancellationToken)).Items[0];

        bool isStreaming = IsStreaming(video.Snippet.LiveBroadcastContent);

        return isStreaming
            ? new YoutubeLive()
            {
                Type = YoutubeTypeEnum.Live,
                ChannelId = channelId,
                Author = channel.Snippet.Title,
                Id = item.ContentDetails.VideoId,
                ThumbnailUrl = GetThumbnailUrl(video.Snippet.Thumbnails),
                Url = "https://www.youtube.com/watch?v=" + item.ContentDetails.VideoId,
                LiveStartTime = video.LiveStreamingDetails.ActualStartTime ?? DateTime.UtcNow
            }
            : new YoutubeVideo()
            {
                Type = YoutubeTypeEnum.Live,
                ChannelId = channelId,
                Author = channel.Snippet.Title,
                Id = item.ContentDetails.VideoId,
                ThumbnailUrl = GetThumbnailUrl(video.Snippet.Thumbnails),
                Url = "https://www.youtube.com/watch?v=" + item.ContentDetails.VideoId,
                PubTime = video.Snippet.PublishedAt ?? throw new YoutubeApiException("Failed to get the pubTime of the latest video!", channelId)     // https://developers.google.com/youtube/v3/docs/videos#snippet.publishedAt
            };
    }

    private static bool IsStreaming(string liveBroadcaseContent)
    {
        return liveBroadcaseContent switch
        {
            "live" => true,
            _ => false
        };
    }

    private static string GetThumbnailUrl(ThumbnailDetails thumbnailDetails)
    {
        return thumbnailDetails.Maxres?.Url
            ?? thumbnailDetails.High?.Url
            ?? thumbnailDetails.Medium?.Url
            ?? thumbnailDetails.Default__.Url;
    }
}
