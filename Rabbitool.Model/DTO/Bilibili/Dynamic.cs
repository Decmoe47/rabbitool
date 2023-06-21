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

public abstract class BaseDynamic
{
    public required uint Uid { get; set; }
    public required string Uname { get; set; }
    public required DynamicTypeEnum DynamicType { get; set; }
    public required string DynamicId { get; set; }
    public required string DynamicUrl { get; set; }
    public required DateTime DynamicUploadTime { get; set; }
}

public class CommonDynamic : BaseDynamic, IOriginDynamic
{
    public required string Text { get; set; }
    public List<string>? ImageUrls { get; set; }
    public Reserve? Reserve { get; set; }
}

public class Reserve
{
    public required string Title { get; set; }
    public required DateTime StartTime { get; set; }
}

public class VideoDynamic : BaseDynamic, IOriginDynamic
{
    public required string DynamicText { get; set; }
    public required string VideoTitle { get; set; }
    public required string VideoThumbnailUrl { get; set; }
    public required string VideoUrl { get; set; }
}

public class ArticleDynamic : BaseDynamic, IOriginDynamic
{
    public required string ArticleTitle { get; set; }
    public required string ArticleThumbnailUrl { get; set; }
    public required string ArticleUrl { get; set; }
}

public class ForwardDynamic : BaseDynamic
{
    public required string DynamicText { get; set; }

    private object _origin = new();

    /// <summary>
    /// 类型可以为<see cref="CommonDynamic"/>、
    /// <see cref="VideoDynamic"/>、
    /// <see cref="ArticleDynamic"/>、
    /// <see cref="string"/>。
    /// </summary>
    public required object Origin
    {
        get => _origin;
        set => _origin = value switch
        {
            IOriginDynamic or string => value,
            _ => throw new InvalidProperityTypeException(
                    $"The type {value.GetType().Name} of Origin is invalid"),
        };
    }
}

public interface IOriginDynamic
{ }

public class LiveCardDynamic : BaseDynamic, IOriginDynamic
{
    public required uint RoomId { get; set; }
    public required LiveStatusEnum LiveStatus { get; set; }
    public required DateTime LiveStartTime { get; set; }
    public required string Title { get; set; }
    public required string CoverUrl { get; set; }
}
