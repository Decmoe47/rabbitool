using System.ComponentModel.DataAnnotations.Schema;

namespace Rabbitool.Model.Entity.Subscribe;

[Table("QQChannelSubscribe")]
public class QQChannelSubscribeEntity : BaseEntity
{
    public string GuildId { get; set; }
    public string GuildName { get; set; }
    public string ChannelId { get; set; }
    public string ChannelName { get; set; }
    public List<BilibiliSubscribeEntity>? BilibiliSubscribes { get; set; }
    public List<TwitterSubscribeEntity>? TwitterSubscribes { get; set; }
    public List<YoutubeSubscribeEntity>? YoutubeSubscribes { get; set; }
    public List<MailSubscribeEntity>? MailSubscribes { get; set; }

    public QQChannelSubscribeEntity(string guildId, string guildName, string channelId, string channelName)
    {
        GuildId = guildId;
        GuildName = guildName;
        ChannelId = channelId;
        ChannelName = channelName;
    }

    public List<TSubscribe>? GetSubscribeProp<TSubscribe>()
        where TSubscribe : ISubscribeEntity
    {
        return typeof(TSubscribe).Name switch
        {
            nameof(BilibiliSubscribeEntity) => (List<TSubscribe>?)(object?)BilibiliSubscribes,
            nameof(TwitterSubscribeEntity) => (List<TSubscribe>?)(object?)TwitterSubscribes,
            nameof(YoutubeSubscribeEntity) => (List<TSubscribe>?)(object?)YoutubeSubscribes,
            nameof(MailSubscribeEntity) => (List<TSubscribe>?)(object?)MailSubscribes,
            _ => null
        };
    }
}

public static class QQChannelSubscribeEntityExtension
{
    public static bool SubscribesAreAllEmpty(this QQChannelSubscribeEntity record)
    {
        return (record.BilibiliSubscribes == null || record.BilibiliSubscribes.Count == 0)
            && (record.YoutubeSubscribes == null || record.YoutubeSubscribes.Count == 0)
            && (record.TwitterSubscribes == null || record.TwitterSubscribes.Count == 0)
            && (record.MailSubscribes == null || record.MailSubscribes.Count == 0);
    }
}
