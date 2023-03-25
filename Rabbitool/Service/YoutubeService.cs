using System.Threading.RateLimiting;
using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using Rabbitool.Model.DTO.Youtube;

namespace Rabbitool.Service;

public class YoutubeService
{
    private readonly YouTubeService _ytb;

    private readonly RateLimiter _limiter = new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
    {
        QueueLimit = 1,
        ReplenishmentPeriod = TimeSpan.FromMinutes(1),
        TokenLimit = 6,
        TokensPerPeriod = 6,
    });     // See https://developers.google.com/youtube/v3/getting-started

    public YoutubeService(string apiKey)
    {
        _ytb = new YouTubeService(new BaseClientService.Initializer
        {
            ApplicationName = "Rabbitool",
            ApiKey = apiKey
        });
    }

    public async Task<YoutubeItem> GetLatestVideoOrLiveAsync(string channelId, CancellationToken ct = default)
    {
        // https://developers.google.com/youtube/v3/docs/channels
        ChannelsResource.ListRequest channelsReq = _ytb.Channels
            .List(new Google.Apis.Util.Repeatable<string>(new string[1] { "contentDetails" }));
        channelsReq.Id = channelId;
        await _limiter.AcquireAsync(1, ct);
        Channel channel = (await channelsReq.ExecuteAsync(ct)).Items[0];

        // https://developers.google.com/youtube/v3/docs/playlistItems
        PlaylistItemsResource.ListRequest playListReq = _ytb.PlaylistItems
            .List(new Google.Apis.Util.Repeatable<string>(new string[1] { "contentDetails" }));
        playListReq.PlaylistId = channel.ContentDetails.RelatedPlaylists.Uploads;
        await _limiter.AcquireAsync(1, ct);
        PlaylistItem item = (await playListReq.ExecuteAsync(ct)).Items[0];

        // https://developers.google.com/youtube/v3/docs/videos
        VideosResource.ListRequest videosReq = _ytb.Videos
            .List(new Google.Apis.Util.Repeatable<string>(new string[2] { "snippet", "liveStreamingDetails" }));
        videosReq.Id = item.ContentDetails.VideoId;
        await _limiter.AcquireAsync(1, ct);
        Video video = (await videosReq.ExecuteAsync(ct)).Items[0];

        return CreateDTO(channelId, item.ContentDetails.VideoId, video);
    }

    public async Task<YoutubeLive?> IsStreamingAsync(string liveRoomId, CancellationToken ct = default)
    {
        VideosResource.ListRequest videosReq = _ytb.Videos
            .List(new Google.Apis.Util.Repeatable<string>(new string[2] { "snippet", "liveStreamingDetails" }));
        videosReq.Id = liveRoomId;
        await _limiter.AcquireAsync(1, ct);
        Video video = (await videosReq.ExecuteAsync(ct)).Items[0];

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
                Title = video.Snippet.Title,
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
                Title = video.Snippet.Title,
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
                Title = video.Snippet.Title,
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
