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

    public async Task<(YoutubeItem first, YoutubeItem second)> GetLatestTwoVideoOrLiveAsync(
        string channelId, CancellationToken cancellationToken = default)
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
        PlaylistItem secondItem = (await playListReq.ExecuteAsync(cancellationToken)).Items[1];

        // https://developers.google.com/youtube/v3/docs/videos
        VideosResource.ListRequest videosReq = _ytb.Videos
            .List(new Google.Apis.Util.Repeatable<string>(new string[4] { "snippet", "contentDetails", "statistics", "liveStreamingDetails" }));
        videosReq.Id = item.ContentDetails.VideoId;
        Video video = (await videosReq.ExecuteAsync(cancellationToken)).Items[0];

        VideosResource.ListRequest videosReq2 = _ytb.Videos
            .List(new Google.Apis.Util.Repeatable<string>(new string[4] { "snippet", "contentDetails", "statistics", "liveStreamingDetails" }));
        videosReq2.Id = secondItem.ContentDetails.VideoId;
        Video secondVideo = (await videosReq2.ExecuteAsync(cancellationToken)).Items[0];

        return (CreateDTO(channelId, item.ContentDetails.VideoId, video), CreateDTO(channelId, secondItem.ContentDetails.VideoId, secondVideo));
    }

    public async Task<YoutubeLive?> IsStreamingAsync(string liveRoomId, CancellationToken cancellationToken = default)
    {
        _limiter.Wait();

        VideosResource.ListRequest videosReq = _ytb.Videos
            .List(new Google.Apis.Util.Repeatable<string>(new string[4] { "snippet", "contentDetails", "statistics", "liveStreamingDetails" }));
        videosReq.Id = liveRoomId;
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
                ActualStartTime = video.LiveStreamingDetails.ActualStartTime ?? DateTime.UtcNow
            },
            "upcoming" => new YoutubeLive()
            {
                Type = YoutubeTypeEnum.UpcomingLive,
                ChannelId = channelId,
                Author = video.Snippet.ChannelTitle,
                Id = itemId,
                ThumbnailUrl = GetThumbnailUrl(video.Snippet.Thumbnails),
                Url = "https://www.youtube.com/watch?v=" + itemId,
                ScheduledStartTime = video.LiveStreamingDetails.ScheduledStartTime
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
                PubTime = video.Snippet.PublishedAt
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
