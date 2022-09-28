using Rabbitool.Common.Util;
using Rabbitool.Model.Entity.Subscribe;

namespace Rabbitool.Service;

public class LimiterCollection
{
    public static LimiterUtil BilibiliLimiter = new(1, 1);
    public static LimiterUtil TwitterUserApiLimiter = new(1, 1);

    /// <summary>
    /// <c>consume</c> choose 1
    /// <para></para>
    /// See https://developer.twitter.com/en/docs/twitter-api/tweets/timelines/migrate
    /// </summary>
    public static LimiterUtil TwitterTweetApiLimiter = new(0.0373f, 100000);

    /// <summary>
    /// See https://developers.google.com/youtube/v3/getting-started?hl=ja
    /// </summary>
    public static LimiterUtil YoutubeApiLimiter = new(0.115f, 10000);

    public static LimiterUtil YoutubeFeedLimiter = new(1, 1);

    public static LimiterUtil QQBotLimiter = new(4, 4);

    public static LimiterUtil GetLimiterBySubscribeEntity<T>()
        where T : ISubscribeEntity
    {
        return typeof(T).Name switch
        {
            nameof(BilibiliSubscribeEntity) => BilibiliLimiter,
            nameof(TwitterSubscribeEntity) => TwitterUserApiLimiter,
            nameof(YoutubeSubscribeEntity) => YoutubeFeedLimiter,
            nameof(QQChannelSubscribeEntity) => QQBotLimiter,
            _ => throw new ArgumentException($"The T {typeof(T).Name} is invalid!")
        };
    }
}
