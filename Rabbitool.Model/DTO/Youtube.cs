namespace Rabbitool.Model.DTO.Youtube;

public enum YoutubeTypeEnum
{
    Video,
    Live,
    UpcomingLive
}

public class YoutubeBase
{
    public required YoutubeTypeEnum Type { get; set; }
    public required string ChannelId { get; set; }
    public required string Author { get; set; }
}

public class YoutubeItem : YoutubeBase
{
    public required string Id { get; set; }
    public required string Title { get; set; }
    public required string ThumbnailUrl { get; set; }
    public required string Url { get; set; }
}

public class YoutubeVideo : YoutubeItem
{
    public required DateTime PubTime { get; set; }
}

public class YoutubeLive : YoutubeItem
{
    public DateTime? ScheduledStartTime { get; set; }
    public DateTime? ActualStartTime { get; set; }
}