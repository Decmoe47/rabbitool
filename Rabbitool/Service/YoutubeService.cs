using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using Rabbitool.Common.Util;
using Rabbitool.Model.DTO.Youtube;

namespace Rabbitool.Service;

public class YoutubeService
{
    private readonly LimiterUtil _limiter = LimiterCollection.YoutubeApiLimiter;
    private readonly YouTubeService _ytb;

    public YoutubeService(string apiKey)
    {
        _ytb = new YouTubeService(new BaseClientService.Initializer
        {
            ApplicationName = "Rabbitool",
            ApiKey = apiKey
        });
    }

    public async Task<YoutubeItem> GetLatestTwoVideoOrLiveAsync(
        string channelId, CancellationToken cancellationToken = default)
    {
        // https://developers.google.com/youtube/v3/docs/channels
        ChannelsResource.ListRequest channelsReq = _ytb.Channels
            .List(new Google.Apis.Util.Repeatable<string>(new string[1] { "contentDetails" }));
        channelsReq.Id = channelId;
        _limiter.Wait(3, cancellationToken);
        Channel channel = (await channelsReq.ExecuteAsync(cancellationToken)).Items[0];

        // https://developers.google.com/youtube/v3/docs/playlistItems
        PlaylistItemsResource.ListRequest playListReq = _ytb.PlaylistItems
            .List(new Google.Apis.Util.Repeatable<string>(new string[1] { "contentDetails" }));
        playListReq.PlaylistId = channel.ContentDetails.RelatedPlaylists.Uploads;
        _limiter.Wait(3, cancellationToken);
        PlaylistItem item = (await playListReq.ExecuteAsync(cancellationToken)).Items[0];

        // https://developers.google.com/youtube/v3/docs/videos
        VideosResource.ListRequest videosReq = _ytb.Videos
            .List(new Google.Apis.Util.Repeatable<string>(new string[2] { "snippet", "liveStreamingDetails" }));
        videosReq.Id = item.ContentDetails.VideoId;
        _limiter.Wait(5, cancellationToken);
        Video video = (await videosReq.ExecuteAsync(cancellationToken)).Items[0];

        return CreateDTO(channelId, item.ContentDetails.VideoId, video);
    }

    public async Task<YoutubeLive?> IsStreamingAsync(string liveRoomId, CancellationToken cancellationToken = default)
    {
        VideosResource.ListRequest videosReq = _ytb.Videos
            .List(new Google.Apis.Util.Repeatable<string>(new string[2] { "snippet", "liveStreamingDetails" }));
        videosReq.Id = liveRoomId;
        _limiter.Wait(5, cancellationToken);
        Video video = (await videosReq.ExecuteAsync(cancellationToken)).Items[0];

        return video.Snippet.LiveBroadcastContent switch
        {
            "live" => (YoutubeLive)CreateDTO(video.Snippet.ChannelId, liveRoomId, video),
            _ => null
        };
    }

    private static YoutubeItem CreateDTO(string channelId, string itemId, Video video)
    {
        return video.Snippet.LiveBroadcastContent switch
        {
            "live" => new YoutubeLive()
            {
                Type = YoutubeTypeEnum.Live,
                ChannelId = channelId,
                Author = video.Snippet.ChannelTitle,
                Id = itemId,
                ThumbnailUrl = GetThumbnailUrl(video.Snippet.Thumbnails),
                Url = "https://www.youtube.com/watch?v=" + itemId,
                ActualStartTime = video.LiveStreamingDetails.ActualStartTime?.ToUniversalTime() ?? DateTime.UtcNow
            },
            "upcoming" => new YoutubeLive()
            {
                Type = YoutubeTypeEnum.UpcomingLive,
                ChannelId = channelId,
                Author = video.Snippet.ChannelTitle,
                Id = itemId,
                ThumbnailUrl = GetThumbnailUrl(video.Snippet.Thumbnails),
                Url = "https://www.youtube.com/watch?v=" + itemId,
                ScheduledStartTime = video.LiveStreamingDetails.ScheduledStartTime?.ToUniversalTime()
                    ?? throw new YoutubeApiException("Failed to get the scheduled start time of the latest live room!", channelId)
            },
            _ => new YoutubeVideo()
            {
                Type = YoutubeTypeEnum.Video,
                ChannelId = channelId,
                Author = video.Snippet.ChannelTitle,
                Id = itemId,
                ThumbnailUrl = GetThumbnailUrl(video.Snippet.Thumbnails),
                Url = "https://www.youtube.com/watch?v=" + itemId,
                PubTime = video.Snippet.PublishedAt?.ToUniversalTime()
                    ?? throw new YoutubeApiException("Failed to get the pubTime of the latest video!", channelId)     // https://developers.google.com/youtube/v3/docs/videos#snippet.publishedAt
            }
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
