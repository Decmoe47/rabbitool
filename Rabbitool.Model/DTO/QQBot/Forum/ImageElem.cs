using Newtonsoft.Json;

namespace Rabbitool.Model.DTO.QQBot;

public class ImageElem
{
    [JsonProperty("third_url")]
    public string ThirdUrl { get; set; } = string.Empty;

    [JsonProperty("width_percent", NullValueHandling = NullValueHandling.Ignore)]
    public double? WidthPercent { get; set; }
}
