using Newtonsoft.Json;

namespace Rabbitool.Model.DTO.QQBot;

public class UrlElem
{
    [JsonProperty("url")]
    public string Url { get; set; } = string.Empty;

    [JsonProperty("desc")]
    public string Desc { get; set; } = string.Empty;
}
