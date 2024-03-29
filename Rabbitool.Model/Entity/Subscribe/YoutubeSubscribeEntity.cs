﻿using System.ComponentModel.DataAnnotations.Schema;

namespace Rabbitool.Model.Entity.Subscribe;

[Table("YoutubeSubscribe")]
public class YoutubeSubscribeEntity(string channelId, string name) : BaseSubscribeEntity, ISubscribeEntity
{
    public string Name { get; set; } = name;
    public string ChannelId { get; set; } = channelId;

    public string LastVideoId { get; set; } = string.Empty;
    public DateTime LastVideoPubTime { get; set; } = new DateTime(1970, 1, 1).ToUniversalTime();

    public string LastLiveRoomId { get; set; } = string.Empty;
    public DateTime LastLiveStartTime { get; set; } = new DateTime(1970, 1, 1).ToUniversalTime();

    public List<string> AllUpcomingLiveRoomIds { get; set; } = [];
    public List<string> AllArchiveVideoIds { get; set; } = [];

    public List<QQChannelSubscribeEntity> QQChannels { get; set; } = [];

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

    private YoutubeSubscribeConfigEntity()
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