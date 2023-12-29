using System.Threading.RateLimiting;
using Google.Apis.Services;
using Google.Apis.Util;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using Rabbitool.Configs;
using Rabbitool.Model.DTO.Youtube;

namespace Rabbitool.Service;

public class YoutubeService
{
    private readonly RateLimiter _limiter = new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
    {
        QueueLimit = 1,
        ReplenishmentPeriod = TimeSpan.FromMinutes(1),
        TokenLimit = 6,
        TokensPerPeriod = 6
    }); // See https://developers.google.com/youtube/v3/getting-started

    private readonly YouTubeService _ytb = new(new BaseClientService.Initializer
    {
        ApplicationName = "Rabbitool",
        ApiKey = Env.R.Youtube!.ApiKey
    });

    public async Task<YoutubeItem> GetLatestVideoOrLiveAsync(string channelId, CancellationToken ct = default)
    {
        // https://developers.google.com/youtube/v3/docs/channels
        ChannelsResource.ListRequest channelsReq = _ytb.Channels
            .List(new Repeatable<string>(new[] { "contentDetails" }));
        channelsReq.Id = channelId;
        await _limiter.AcquireAsync(1, ct);
        Channel channel = (await channelsReq.ExecuteAsync(ct)).Items[0];

        // https://developers.google.com/youtube/v3/docs/playlistItems
        PlaylistItemsResource.ListRequest playListReq = _ytb.PlaylistItems
            .List(new Repeatable<string>(new[] { "contentDetails" }));
        playListReq.PlaylistId = channel.ContentDetails.RelatedPlaylists.Uploads;
        await _limiter.AcquireAsync(1, ct);
        IList<PlaylistItem> playlistItems = (await playListReq.ExecuteAsync(ct)).Items;

        Video video = new();
        foreach (PlaylistItem playlistItem in playlistItems)
        {
            // https://developers.google.com/youtube/v3/docs/videos
            VideosResource.ListRequest videosReq = _ytb.Videos
                .List(new Repeatable<string>(new[] { "snippet", "liveStreamingDetails" }));
            videosReq.Id = playlistItem.ContentDetails.VideoId;
            await _limiter.AcquireAsync(1, ct);
            video = (await videosReq.ExecuteAsync(ct)).Items[0];

            if (video.LiveStreamingDetails?.ScheduledStartTimeDateTimeOffset != null)
                break;
        }

        return CreateDto(channelId, video.Id, video);
    }

    public async Task<YoutubeLive?> IsStreamingAsync(string liveRoomId, CancellationToken ct = default)
    {
        VideosResource.ListRequest videosReq = _ytb.Videos
            .List(new Repeatable<string>(new[] { "snippet", "liveStreamingDetails" }));
        videosReq.Id = liveRoomId;
        await _limiter.AcquireAsync(1, ct);
        IList<Video> videos = (await videosReq.ExecuteAsync(ct)).Items;
        Video video;
        if (videos?.Count is > 0)
            video = videos[0];
        else
            return null;

        return video.Snippet.LiveBroadcastContent switch
        {
            "live" => (YoutubeLive)CreateDto(video.Snippet.ChannelId, liveRoomId, video),
            _ => null
        };
    }

    private static YoutubeItem CreateDto(string channelId, string itemId, Video video)
    {
        return video.Snippet.LiveBroadcastContent switch
        {
            "live" => new YoutubeLive
            {
                Type = YoutubeTypeEnum.Live,
                ChannelId = channelId,
                Author = video.Snippet.ChannelTitle,
                Id = itemId,
                Title = video.Snippet.Title,
                ThumbnailUrl = GetThumbnailUrl(video.Snippet.Thumbnails),
                Url = "https://www.youtube.com/watch?v=" + itemId,
                ActualStartTime =
                    video.LiveStreamingDetails.ActualStartTimeDateTimeOffset?.DateTime ??
                    DateTime.UtcNow
            },
            "upcoming" => new YoutubeLive
            {
                Type = YoutubeTypeEnum.UpcomingLive,
                ChannelId = channelId,
                Author = video.Snippet.ChannelTitle,
                Id = itemId,
                Title = video.Snippet.Title,
                ThumbnailUrl = GetThumbnailUrl(video.Snippet.Thumbnails),
                Url = "https://www.youtube.com/watch?v=" + itemId,
                ScheduledStartTime =
                    video.LiveStreamingDetails.ScheduledStartTimeDateTimeOffset?.DateTime
                    ?? throw new YoutubeApiException(
                        "Failed to get the scheduled start time of the latest live room!", channelId)
            },
            _ => new YoutubeVideo
            {
                Type = YoutubeTypeEnum.Video,
                ChannelId = channelId,
                Author = video.Snippet.ChannelTitle,
                Id = itemId,
                Title = video.Snippet.Title,
                ThumbnailUrl = GetThumbnailUrl(video.Snippet.Thumbnails),
                Url = "https://www.youtube.com/watch?v=" + itemId,
                PubTime = video.Snippet.PublishedAtDateTimeOffset?.DateTime
                          ?? throw new YoutubeApiException("Failed to get the pubTime of the latest video!",
                              channelId) // https://developers.google.com/youtube/v3/docs/videos#snippet.publishedAt
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