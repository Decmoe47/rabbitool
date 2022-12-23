using Newtonsoft.Json;

namespace Rabbitool.Model.DTO.QQBot;

public class Paragraph
{
    [JsonProperty("elems", NullValueHandling = NullValueHandling.Ignore)]
    public List<Elem>? Elems { get; set; } = new List<Elem>();

    [JsonProperty("props")]
    public ParagraphProps Props { get; set; } = new ParagraphProps();
}
