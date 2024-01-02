using System.ComponentModel.DataAnnotations.Schema;

namespace Rabbitool.Model.Entity.Subscribe;

[Table("MailSubscribe")]
public class MailSubscribeEntity(
    string username,
    string address,
    string password,
    string host,
    int port,
    string mailbox = "INBOX",
    bool ssl = false)
    : BaseSubscribeEntity, ISubscribeEntity
{
    public string Username { get; set; } = username;
    public string Address { get; set; } = address;
    public string Password { get; set; } = password;
    public string Mailbox { get; set; } = mailbox;
    public string Host { get; set; } = host;
    public int Port { get; set; } = port;
    public bool Ssl { get; set; } = ssl;

    public DateTime LastMailTime { get; set; } = new DateTime(1970, 1, 1).ToUniversalTime();

    public List<QQChannelSubscribeEntity> QQChannels { get; set; } = [];

    [NotMapped] public string PropName { get; set; } = "MailSubscribes";

    public string GetInfo(string separator)
    {
        string result = "username=" + Username + separator;
        result += "address=" + Address + separator;
        result += "host=" + Host.Replace(".", "*") + separator;
        result += "port=" + Port + separator;
        result += "mailbox=" + Mailbox + separator;
        result += "ssl=" + Ssl.ToString().ToLower();

        return result;
    }

    public string GetId()
    {
        return Username;
    }
}

[Table("MailSubscribeConfig")]
public class MailSubscribeConfigEntity : BaseSubscribeConfigEntity<MailSubscribeEntity>, ISubscribeConfigEntity
{
    public MailSubscribeConfigEntity(
        QQChannelSubscribeEntity qqChannel,
        MailSubscribeEntity subscribe) : base(qqChannel, subscribe)
    {
    }

    private MailSubscribeConfigEntity()
    {
    }

    public bool Detail { get; set; }
    public bool PushToThread { get; set; }

    public string GetConfigs(string separator)
    {
        string result = "pushToThread=" + PushToThread.ToString().ToLower();
        return result;
    }
}