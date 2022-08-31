using Newtonsoft.Json;

namespace Rabbitool.Model.DTO.QQBot;

public class Elem
{
    [JsonProperty("text", NullValueHandling = NullValueHandling.Ignore)]
    public TextElem? Text { get; set; }

    [JsonProperty("image", NullValueHandling = NullValueHandling.Ignore)]
    public ImageElem? Image { get; set; }

    [JsonProperty("video", NullValueHandling = NullValueHandling.Ignore)]
    public VideoElem? Video { get; set; }

    [JsonProperty("url", NullValueHandling = NullValueHandling.Ignore)]
    public UrlElem? Url { get; set; }

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
