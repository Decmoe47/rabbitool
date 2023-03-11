using Newtonsoft.Json;

namespace Rabbitool.Model.DTO.QQBot;

public class Paragraph
{
    [JsonProperty("elems", NullValueHandling = NullValueHandling.Ignore)]
    public List<ElemDTO>? Elems { get; set; } = new List<ElemDTO>();

    [JsonProperty("props")]
    public ParagraphProps Props { get; set; } = new ParagraphProps();
}
