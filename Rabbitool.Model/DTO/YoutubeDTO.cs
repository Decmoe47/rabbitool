namespace Rabbitool.Model.DTO.Youtube;

public enum YoutubeTypeEnum
{
    Video,
    Live
}

public class YoutubeBase
{
    public YoutubeTypeEnum Type { get; set; }
    public string ChannelId { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
}

public class YoutubeVideo : YoutubeBase
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string ThumbnailUrl { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public DateTime PubTime { get; set; }
}

public class YoutubeLive : YoutubeBase
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string ThumbnailUrl { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public DateTime LiveStartTime { get; set; }
}
