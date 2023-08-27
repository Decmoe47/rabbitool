using System.ComponentModel.DataAnnotations.Schema;

namespace Rabbitool.Model.Entity.Subscribe;

[Table("TwitterSubscribe")]
public class TwitterSubscribeEntity : BaseSubscribeEntity, ISubscribeEntity
{
    public TwitterSubscribeEntity(string screenName, string name)
    {
        Name = name;
        ScreenName = screenName;
    }

    public string Name { get; set; }
    public string ScreenName { get; set; }
    public string LastTweetId { get; set; } = string.Empty;
    public DateTime LastTweetTime { get; set; } = new DateTime(1970, 1, 1).ToUniversalTime();

    public List<QQChannelSubscribeEntity> QQChannels { get; set; } = new();

    [NotMapped] public string PropName { get; set; } = "TwitterSubscribes";

    public string GetInfo(string separator)
    {
        string result = "screenName=" + ScreenName + separator;
        result += "name=" + Name;
        return result;
    }

    public string GetId()
    {
        return ScreenName;
    }
}

[Table("TwitterSubscribeConfig")]
public class TwitterSubscribeConfigEntity : BaseSubscribeConfigEntity<TwitterSubscribeEntity>, ISubscribeConfigEntity
{
    public TwitterSubscribeConfigEntity(
        QQChannelSubscribeEntity qqChannel,
        TwitterSubscribeEntity subscribe) : base(qqChannel, subscribe)
    {
    }

    public bool RtPush { get; set; }
    public bool PushToThread { get; set; }

    public string GetConfigs(string separator)
    {
        string result = "rtPush=" + RtPush.ToString().ToLower() + separator;
        result += "pushToThread=" + PushToThread.ToString().ToLower();
        return result;
    }
}