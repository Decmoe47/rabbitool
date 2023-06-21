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

    public List<T>? GetSubscribeProp<T>() where T : ISubscribeEntity
    {
        return typeof(T).Name switch
        {
            nameof(BilibiliSubscribeEntity) => (List<T>?)(object?)BilibiliSubscribes,
            nameof(TwitterSubscribeEntity) => (List<T>?)(object?)TwitterSubscribes,
            nameof(YoutubeSubscribeEntity) => (List<T>?)(object?)YoutubeSubscribes,
            nameof(MailSubscribeEntity) => (List<T>?)(object?)MailSubscribes,
            _ => null
        };
    }

    public bool ContainsSubscribe<T>(T subscribe) where T : ISubscribeEntity
    {
        List<T>? prop = GetSubscribeProp<T>();
        return prop != null && prop.Exists(p => p.GetId() == subscribe.GetId());
    }

    public void AddSubscribe<T>(T subscribe) where T : ISubscribeEntity
    {
        if (subscribe is BilibiliSubscribeEntity b)
        {
            if (BilibiliSubscribes == null)
                BilibiliSubscribes = new List<BilibiliSubscribeEntity>();
            else
                BilibiliSubscribes.Add(b);
        }
        else if (subscribe is TwitterSubscribeEntity t)
        {
            if (TwitterSubscribes == null)
                TwitterSubscribes = new List<TwitterSubscribeEntity>();
            else
                TwitterSubscribes.Add(t);
        }
        else if (subscribe is YoutubeSubscribeEntity y)
        {
            if (YoutubeSubscribes == null)
                YoutubeSubscribes = new List<YoutubeSubscribeEntity>();
            else
                YoutubeSubscribes.Add(y);
        }
        else if (subscribe is MailSubscribeEntity m)
        {
            if (MailSubscribes == null)
                MailSubscribes = new List<MailSubscribeEntity>();
            else
                MailSubscribes.Add(m);
        }
    }

    public void RemoveSubscribe<T>(T subscribe) where T : ISubscribeEntity
    {
        if (subscribe is BilibiliSubscribeEntity b)
            BilibiliSubscribes?.RemoveAll(b => b.GetId() == subscribe.GetId());
        else if (subscribe is TwitterSubscribeEntity t)
            TwitterSubscribes?.RemoveAll(t => t.GetId() == subscribe.GetId());
        else if (subscribe is YoutubeSubscribeEntity y)
            YoutubeSubscribes?.RemoveAll(y => y.GetId() == subscribe.GetId());
        else if (subscribe is MailSubscribeEntity m)
            MailSubscribes?.RemoveAll(m => m.GetId() == subscribe.GetId());
    }

    public bool SubscribesAreAllEmpty()
    {
        return (BilibiliSubscribes == null || BilibiliSubscribes.Count == 0)
            && (TwitterSubscribes == null || TwitterSubscribes.Count == 0)
            && (YoutubeSubscribes == null || YoutubeSubscribes.Count == 0)
            && (MailSubscribes == null || MailSubscribes.Count == 0);
    }
}
