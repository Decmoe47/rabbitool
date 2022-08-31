using System.ComponentModel.DataAnnotations.Schema;

namespace Rabbitool.Model.Entity.Subscribe;

[Table("YoutubeSubscribe")]
public class YoutubeSubscribeEntity : BaseSubscribeEntity, ISubscribeEntity
{
    public string Name { get; set; }
    public string ChannelId { get; set; }

    public string LastVideoOrLiveId { get; set; } = string.Empty;
    public DateTime LastVideoOrLiveTime { get; set; } = new DateTime(1970, 1, 1).ToUniversalTime();

    public List<string>? AllArchiveVideoIds { get; set; }

    public List<QQChannelSubscribeEntity> QQChannels { get; set; } = new List<QQChannelSubscribeEntity>();

    public YoutubeSubscribeEntity(string channelId, string name)
    {
        Name = name;
        ChannelId = channelId;
    }

    public string GetInfo(string separator)
    {
        string result = "channelId=" + ChannelId + separator;
        result += "name=" + Name;
        return result;
    }

    public string GetId()
    {
        return ChannelId;
    }
}

[Table("YoutubeSubscribeConfig")]
public class YoutubeSubscribeConfigEntity : BaseSubscribeConfigEntity<YoutubeSubscribeEntity>, ISubscribeConfigEntity
{
    public bool ArchivePush { get; set; } = false;

    private YoutubeSubscribeConfigEntity()
    {
    }

    public YoutubeSubscribeConfigEntity(
        QQChannelSubscribeEntity qqChannel,
        YoutubeSubscribeEntity subscribe) : base(qqChannel, subscribe)
    {
    }

    public string GetConfigs(string separator)
    {
        string result = "archivePush=" + ArchivePush.ToString().ToLower();
        return result;
    }
}
