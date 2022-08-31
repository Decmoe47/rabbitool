using Rabbitool.Common.Util;
using Rabbitool.Model.Entity.Subscribe;

namespace Rabbitool.Service;

public class LimiterCollection
{
    public static LimiterUtil BilibiliLimter = new(5, 5);
    public static LimiterUtil TwitterLimter = new(1, 1);

    /// <summary>
    /// comsume choose 10
    /// </summary>
    public static LimiterUtil YoutubeLimter = new(1, 10);

    public static LimiterUtil QQBotLimter = new(5, 5);

    public static LimiterUtil GetLimterBySubscribeEntity<T>()
        where T : ISubscribeEntity
    {
        return typeof(T).Name switch
        {
            nameof(BilibiliSubscribeEntity) => BilibiliLimter,
            nameof(TwitterSubscribeEntity) => TwitterLimter,
            nameof(YoutubeSubscribeEntity) => YoutubeLimter,
            nameof(QQChannelSubscribeEntity) => QQBotLimter,
            _ => throw new ArgumentException($"The T {typeof(T).Name} is invalid!")
        };
    }
}
