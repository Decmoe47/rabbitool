using Newtonsoft.Json;

namespace Rabbitool.Model.DTO.QQBot;

public class ElemDTO
{
    [JsonProperty("text", NullValueHandling = NullValueHandling.Ignore)]
    public TextElemDTO? Text { get; set; }

    [JsonProperty("image", NullValueHandling = NullValueHandling.Ignore)]
    public ImageElemDTO? Image { get; set; }

    [JsonProperty("video", NullValueHandling = NullValueHandling.Ignore)]
    public VideoElemDTO? Video { get; set; }

    [JsonProperty("url", NullValueHandling = NullValueHandling.Ignore)]
    public UrlElemDTO? Url { get; set; }

    [JsonProperty("type")]
    public ElemTypeEnum Type { get; set; }
}

public enum ElemTypeEnum
{
    Text = 1,
    Image,
    Video,
    Url
}
