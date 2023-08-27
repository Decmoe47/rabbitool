namespace Rabbitool.Model.Entity.Subscribe;

public interface ISubscribeEntity : IEntity
{
    string PropName { get; set; }
    List<QQChannelSubscribeEntity> QQChannels { get; set; }

    /// <summary>
    /// 此id是指用户的唯一id，不是指数据库主键
    /// </summary>
    /// <returns></returns> 
    string GetId();

    string GetInfo(string separator);
}

public interface ISubscribeConfigEntity : IEntity
{
    string GetConfigs(string separator);
}
