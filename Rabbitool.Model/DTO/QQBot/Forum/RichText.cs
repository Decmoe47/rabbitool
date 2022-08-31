using Newtonsoft.Json;

namespace Rabbitool.Model.DTO.QQBot;

public class RichText
{
    [JsonProperty("paragraphs")]
    public List<Paragraph> Paragraphs { get; set; } = new List<Paragraph>();
}
