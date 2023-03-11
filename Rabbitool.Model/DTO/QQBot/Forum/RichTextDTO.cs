using Newtonsoft.Json;

namespace Rabbitool.Model.DTO.QQBot;

public class RichTextDTO
{
    [JsonProperty("paragraphs")]
    public List<ParagraphDTO> Paragraphs { get; set; } = new List<ParagraphDTO>();
}
