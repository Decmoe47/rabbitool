using System.ComponentModel.DataAnnotations.Schema;
using Rabbitool.Model.DTO.Bilibili;

namespace Rabbitool.Model.Entity.Subscribe;

[Table("BilibiliSubscribe")]
public class BilibiliSubscribeEntity : BaseSubscribeEntity, ISubscribeEntity
{
    public uint Uid { get; set; }
    public string Uname { get; set; }

    public DateTime LastDynamicTime { get; set; } = new DateTime(1970, 1, 1).ToUniversalTime();

    public DynamicTypeEnum LastDynamicType { get; set; } = DynamicTypeEnum.TextOnly;

    public LiveStatusEnum LastLiveStatus { get; set; } = LiveStatusEnum.NoLiveStream;

    public List<QQChannelSubscribeEntity> QQChannels { get; set; } = new List<QQChannelSubscribeEntity>();

    public BilibiliSubscribeEntity(uint uid, string uname)
    {
        Uid = uid;
        Uname = uname;
    }

    public string GetInfo(string separator)
    {
        string result = "uid=" + Uid.ToString() + separator;
        result += "uname=" + Uname;
        return result;
    }

    public string GetId()
    {
        return Uid.ToString();
    }

    public bool ContainsQQChannel(string channelId)
    {
        return QQChannels.Find(x => x.ChannelId == channelId) is not null;
    }

    public void RemoveQQChannel(string channelId)
    {
        QQChannelSubscribeEntity? channel = QQChannels.Find(x => x.ChannelId == channelId);
        if (channel != null)
            QQChannels.Remove(channel);
    }
}

[Table("BilibiliSubscribeConfig")]
public class BilibiliSubscribeConfigEntity : BaseSubscribeConfigEntity<BilibiliSubscribeEntity>, ISubscribeConfigEntity
{
    public bool LivePush { get; set; } = true;
    public bool DynamicPush { get; set; } = true;
    public bool PureForwardDynamicPush { get; set; } = false;
    public bool LiveEndingPush { get; set; } = false;

    private BilibiliSubscribeConfigEntity()
    {
    }

    public BilibiliSubscribeConfigEntity(
        QQChannelSubscribeEntity qqChannel,
        BilibiliSubscribeEntity subscribe
    ) : base(qqChannel, subscribe)
    {
    }

    public string GetConfigs(string separator)
    {
        string result = "livePush=" + LivePush.ToString().ToLower() + separator;
        result += "dynamicPush=" + DynamicPush.ToString().ToLower() + separator;
        result += "pureForwardDynamicPush=" + PureForwardDynamicPush.ToString().ToLower() + separator;
        result += "LiveEndingPush=" + LiveEndingPush.ToString().ToLower();
        return result;
    }
}
