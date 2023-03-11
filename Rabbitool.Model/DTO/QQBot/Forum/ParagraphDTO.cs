using Newtonsoft.Json;

namespace Rabbitool.Model.DTO.QQBot;

public class ParagraphDTO
{
    [JsonProperty("elems", NullValueHandling = NullValueHandling.Ignore)]
    public List<ElemDTO>? Elems { get; set; } = new List<ElemDTO>();

    [JsonProperty("props")]
    public ParagraphPropsDTO Props { get; set; } = new ParagraphPropsDTO();
}
