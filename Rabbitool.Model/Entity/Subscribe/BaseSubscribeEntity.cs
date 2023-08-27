namespace Rabbitool.Model.Entity.Subscribe;

public abstract class BaseSubscribeEntity : BaseEntity
{
}

public abstract class BaseSubscribeConfigEntity<T> : BaseEntity where T : BaseSubscribeEntity
{
    protected BaseSubscribeConfigEntity()
    {
    }

    protected BaseSubscribeConfigEntity(QQChannelSubscribeEntity qqChannel, T subscribe) : this()
    {
        QQChannel = qqChannel;
        Subscribe = subscribe;
    }

    public QQChannelSubscribeEntity QQChannel { get; set; } = null!;
    public T Subscribe { get; set; } = null!;
}