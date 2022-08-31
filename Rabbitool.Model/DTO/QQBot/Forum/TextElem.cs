using Newtonsoft.Json;

namespace Rabbitool.Model.DTO.QQBot;

public class TextElem
{
    [JsonProperty("text")]
    public string Text { get; set; } = string.Empty;

    [JsonProperty("props", NullValueHandling = NullValueHandling.Ignore)]
    public TextProps? Props { get; set; }
}

public class TextProps
{
    [JsonProperty("font_bold", NullValueHandling = NullValueHandling.Ignore)]
    public bool? FontBold { get; set; }

    [JsonProperty("italic", NullValueHandling = NullValueHandling.Ignore)]
    public bool? Italic { get; set; }

    [JsonProperty("underline", NullValueHandling = NullValueHandling.Ignore)]
    public bool? Underline { get; set; }
}
