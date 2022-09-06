namespace Rabbitool.Model.DTO.Youtube;

public enum YoutubeTypeEnum
{
    Video,
    Live,
    UpcomingLive
}

public class YoutubeBase
{
    public YoutubeTypeEnum Type { get; set; }
    public string ChannelId { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
}

public class YoutubeItem : YoutubeBase
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string ThumbnailUrl { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}

public class YoutubeVideo : YoutubeItem
{
    public DateTime PubTime { get; set; }
}

public class YoutubeLive : YoutubeItem
{
    public DateTime? ScheduledStartTime { get; set; }
    public DateTime? ActualStartTime { get; set; }
}
