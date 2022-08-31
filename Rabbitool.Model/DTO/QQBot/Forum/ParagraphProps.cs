using Newtonsoft.Json;

namespace Rabbitool.Model.DTO.QQBot;

public class ParagraphProps
{
    [JsonProperty("alignment")]
    public AlignmentEnum Alignment { get; set; }
}

public enum AlignmentEnum
{
    Left,
    Middle,
    Right
}
