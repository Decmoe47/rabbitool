namespace Rabbitool.Model.DTO.Twitter;

public enum TweetTypeEnum
{
    Common,
    RT,
    Quote
}

public class Tweet
{
    public required TweetTypeEnum Type { get; set; }
    public required string Id { get; set; }
    public required string Url { get; set; }

    public required DateTime PubTime { get; set; }
    public required string Author { get; set; }
    public required string AuthorScreenName { get; set; }

    public required string Text { get; set; }
    public List<string>? ImageUrls { get; set; }
    public string? VideoUrl { get; set; }

    public Tweet? Origin { get; set; }
}
