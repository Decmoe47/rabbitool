using System.ComponentModel.DataAnnotations;

namespace Rabbitool.Configs;

public class MarkdownTemplateIds
{
    [Required] public required string TextOnly { get; set; }

    [Required] public required string WithImage { get; set; }

    [Required] public required string ContainsOriginTextOnly { get; set; }

    [Required] public required string ContainsOriginWithImage { get; set; }
}