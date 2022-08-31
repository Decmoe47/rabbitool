namespace Rabbitool.Model.DTO.Twitter;

public enum TweetTypeEnum
{
    Common,
    RT,
    Quote
}

public class Tweet
{
    public TweetTypeEnum Type { get; set; }
    public string Id { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;

    public DateTime PubTime { get; set; }
    public string Author { get; set; } = string.Empty;
    public string AuthorScreenName { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;
    public List<string>? ImageUrls { get; set; }
    public bool HasVideo { get; set; }

    public Tweet? Origin { get; set; }
}
