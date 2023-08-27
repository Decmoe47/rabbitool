using System.ComponentModel.DataAnnotations.Schema;

namespace Rabbitool.Model.Entity.Subscribe;

[Table("YoutubeSubscribe")]
public class YoutubeSubscribeEntity : BaseSubscribeEntity, ISubscribeEntity
{
    public YoutubeSubscribeEntity(string channelId, string name)
    {
        Name = name;
        ChannelId = channelId;
    }

    public string Name { get; set; }
    public string ChannelId { get; set; }

    public string LastVideoId { get; set; } = string.Empty;
    public DateTime LastVideoPubTime { get; set; } = new DateTime(1970, 1, 1).ToUniversalTime();

    public string LastLiveRoomId { get; set; } = string.Empty;
    public DateTime LastLiveStartTime { get; set; } = new DateTime(1970, 1, 1).ToUniversalTime();

    public List<string> AllUpcomingLiveRoomIds { get; set; } = new();
    public List<string> AllArchiveVideoIds { get; set; } = new();

    public List<QQChannelSubscribeEntity> QQChannels { get; set; } = new();

    [NotMapped] public string PropName { get; set; } = "YoutubeSubscribes";

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
    public YoutubeSubscribeConfigEntity(
        QQChannelSubscribeEntity qqChannel,
        YoutubeSubscribeEntity subscribe) : base(qqChannel, subscribe)
    {
    }

    public bool VideoPush { get; set; } = true;
    public bool LivePush { get; set; } = true;
    public bool UpcomingLivePush { get; set; } = true;
    public bool ArchivePush { get; set; }

    public string GetConfigs(string separator)
    {
        string result = "videoPush=" + VideoPush.ToString().ToLower() + separator;
        result += "livePush=" + LivePush.ToString().ToLower() + separator;
        result += "upcomingLivePush=" + UpcomingLivePush.ToString().ToLower() + separator;
        result += "archivePush=" + ArchivePush.ToString().ToLower();

        return result;
    }
}