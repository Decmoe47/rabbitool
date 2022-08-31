using Rabbitool.Common.Exception;

namespace Rabbitool.Model.DTO.Bilibili;

public enum DynamicTypeEnum
{
    TextOnly = 4,
    WithImage = 2,
    Video = 8,
    Article = 64,
    Forward = 1,
    PureForward = 3,    // 自己定义的，不是api里有的
    WebActivity = 2042,
    LiveCard = 4200     // 目前来看，只会出现在origin里
}

public abstract class BaseDynamicDTO
{
    public uint Uid { get; set; }
    public string Uname { get; set; } = string.Empty;
    public DynamicTypeEnum DynamicType { get; set; }
    public string DynamicId { get; set; } = string.Empty;
    public string DynamicUrl { get; set; } = string.Empty;
    public DateTime DynamicUploadTime { get; set; }
}

public class CommonDynamicDTO : BaseDynamicDTO, IOriginDynamicDTO
{
    public string Text { get; set; } = string.Empty;
    public List<string>? ImageUrls { get; set; }
    public ReserveDTO? Reserve { get; set; }
}

public class ReserveDTO
{
    public string Title { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
}

public class VideoDynamicDTO : BaseDynamicDTO, IOriginDynamicDTO
{
    public string DynamicText { get; set; } = string.Empty;
    public string VideoTitle { get; set; } = string.Empty;
    public string VideoThumbnailUrl { get; set; } = string.Empty;
    public string VideoUrl { get; set; } = string.Empty;
}

public class ArticleDynamicDTO : BaseDynamicDTO, IOriginDynamicDTO
{
    public string ArticleTitle { get; set; } = string.Empty;
    public string ArticleThumbnailUrl { get; set; } = string.Empty;
    public string ArticleUrl { get; set; } = string.Empty;
}

public class ForwardDynamicDTO : BaseDynamicDTO
{
    public string DynamicText { get; set; } = string.Empty;

    private object _origin = new();

    /// <summary>
    /// 类型可以为<see cref="CommonDynamicDTO"/>、
    /// <see cref="VideoDynamicDTO"/>、
    /// <see cref="ArticleDynamicDTO"/>、
    /// <see cref="string"/>。
    /// </summary>
    public object Origin
    {
        get => _origin;
        set => _origin = value switch
        {
            IOriginDynamicDTO or string => value,
            _ => throw new InvalidProperityTypeException(
                    $"The type {value.GetType().Name} of Origin is invalid"),
        };
    }
}

public interface IOriginDynamicDTO
{ }

public class LiveCardDynamicDTO : BaseDynamicDTO, IOriginDynamicDTO
{
    public uint RoomId { get; set; }
    public LiveStatusEnum LiveStatus { get; set; }
    public DateTime LiveStartTime { get; set; }
    public string Title { get; set; } = string.Empty;
    public string CoverUrl { get; set; } = string.Empty;
}

public enum LiveStatusEnum
{
    Streaming = 1,
    NoLiveStream = 0,
    Replay = 2
}

public class Live
{
    public uint Uid { get; set; }
    public string Uname { get; set; } = string.Empty;
    public uint RoomId { get; set; }

    public LiveStatusEnum LiveStatus { get; set; }
    public DateTime? LiveStartTime { get; set; }
    public string? Title { get; set; }
    public string? CoverUrl { get; set; }
}
