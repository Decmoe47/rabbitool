using System.ComponentModel.DataAnnotations.Schema;

namespace Rabbitool.Model.Entity.Subscribe;

[Table("MailSubscribe")]
public class MailSubscribeEntity : BaseSubscribeEntity, ISubscribeEntity
{
    public string Address { get; set; }
    public string Password { get; set; }
    public string Mailbox { get; set; }
    public string Host { get; set; }
    public int Port { get; set; }
    public bool Ssl { get; set; } = false;

    public DateTime LastMailTime { get; set; } = new DateTime(1970, 1, 1).ToUniversalTime();

    public List<QQChannelSubscribeEntity> QQChannels { get; set; } = new List<QQChannelSubscribeEntity>();

    public MailSubscribeEntity(
        string address, string password, string host, int port, string mailbox = "INBOX", bool ssl = false)
    {
        Address = address;
        Password = password;
        Mailbox = mailbox;
        Host = host;
        Port = port;
        Ssl = ssl;
    }

    public string GetInfo(string separator)
    {
        string result = "address=" + Address;
        return result;
    }

    public string GetId()
    {
        return Address;
    }
}

[Table("MailSubscribeConfig")]
public class MailSubscribeConfigEntity : BaseSubscribeConfigEntity<MailSubscribeEntity>, ISubscribeConfigEntity
{
    public bool PushToThread { get; set; } = false;

    private MailSubscribeConfigEntity()
    {
    }

    public MailSubscribeConfigEntity(
        QQChannelSubscribeEntity qqChannel,
        MailSubscribeEntity subscribe) : base(qqChannel, subscribe)
    {
    }

    public string GetConfigs(string separator)
    {
        string result = "pushToThread=" + PushToThread.ToString().ToLower();
        return result;
    }
}
