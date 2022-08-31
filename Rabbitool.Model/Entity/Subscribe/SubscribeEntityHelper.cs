namespace Rabbitool.Model.Entity.Subscribe;

public static class SubscribeEntityHelper
{
    public static TSubscribe NewSubscribeEntity<TSubscribe>(string id, string name)
        where TSubscribe : ISubscribeEntity
    {
        switch (typeof(TSubscribe).Name)
        {
            case nameof(BilibiliSubscribeEntity):
                uint uidUint = uint.Parse(id);
                return (TSubscribe)(object)new BilibiliSubscribeEntity(uidUint, name);

            case nameof(TwitterSubscribeEntity):
                return (TSubscribe)(object)new TwitterSubscribeEntity(id, name);

            case nameof(YoutubeSubscribeEntity):
                return (TSubscribe)(object)new YoutubeSubscribeEntity(id, name);

            default:
                throw new NotSupportedException(
                    $"The type TSubscribe which is {typeof(TSubscribe).Name} cann't be generated in this function!");
        }
    }

    public static TConfig NewSubscribeConfigEntity<TSubscribe, TConfig>(
        QQChannelSubscribeEntity qqChannel,
        TSubscribe subscribe)
        where TSubscribe : ISubscribeEntity
        where TConfig : ISubscribeConfigEntity
    {
        if (typeof(TConfig) == typeof(BilibiliSubscribeConfigEntity))
        {
            if (subscribe is BilibiliSubscribeEntity s)
                return (TConfig)(object)new BilibiliSubscribeConfigEntity(qqChannel, s);
            else
                goto Mismatched;
        }
        else if (typeof(TConfig) == typeof(TwitterSubscribeConfigEntity))
        {
            if (subscribe is TwitterSubscribeEntity s)
                return (TConfig)(object)new TwitterSubscribeConfigEntity(qqChannel, s);
            else
                goto Mismatched;
        }
        else if (typeof(TConfig) == typeof(YoutubeSubscribeConfigEntity))
        {
            if (subscribe is YoutubeSubscribeEntity s)
                return (TConfig)(object)new YoutubeSubscribeConfigEntity(qqChannel, s);
            else
                goto Mismatched;
        }
        else if (typeof(TConfig) == typeof(MailSubscribeConfigEntity))
        {
            if (subscribe is MailSubscribeEntity s)
                return (TConfig)(object)new MailSubscribeConfigEntity(qqChannel, s);
            else
                goto Mismatched;
        }
        else
        {
            throw new NotSupportedException(
                $"The type TConfig which is {typeof(TConfig)} cann't be generated in this function!");
        }

    Mismatched: throw new ArgumentException(
        $"The type TSubscribe {typeof(TSubscribe)} isn't match to type TConfig {typeof(TConfig)}");
    }
}
