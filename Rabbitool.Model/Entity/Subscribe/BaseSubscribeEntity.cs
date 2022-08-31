namespace Rabbitool.Model.Entity.Subscribe;

public abstract class BaseSubscribeEntity : BaseEntity
{
}

public abstract class BaseSubscribeConfigEntity<T> : BaseEntity where T : BaseSubscribeEntity
{
    public QQChannelSubscribeEntity QQChannel { get; set; } = null!;
    public T Subscribe { get; set; } = null!;

    protected BaseSubscribeConfigEntity()
    {
    }

    public BaseSubscribeConfigEntity(QQChannelSubscribeEntity qqChannel, T subscribe) : this()
    {
        QQChannel = qqChannel;
        Subscribe = subscribe;
    }
}
