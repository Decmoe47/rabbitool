using System.ComponentModel.DataAnnotations.Schema;

namespace Rabbitool.Model.Entity.Subscribe;

[Table("QQChannelSubscribe")]
public class QQChannelSubscribeEntity : BaseEntity
{
    public string ChannelId { get; set; }
    public string ChannelName { get; set; }
    public List<BilibiliSubscribeEntity>? BilibiliSubscribes { get; set; }
    public List<TwitterSubscribeEntity>? TwitterSubscribes { get; set; }
    public List<YoutubeSubscribeEntity>? YoutubeSubscribes { get; set; }
    public List<MailSubscribeEntity>? MailSubscribes { get; set; }

    public QQChannelSubscribeEntity(string channelId, string channelName)
    {
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
