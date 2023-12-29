using System.ComponentModel.DataAnnotations;

namespace Rabbitool.Common.Configs;

public class MarkdownTemplateIds
{
    [Required] public required string TextOnly { get; init; }

    [Required] public required string WithImage { get; init; }

    [Required] public required string ContainsOriginTextOnly { get; init; }

    [Required] public required string ContainsOriginWithImage { get; init; }
}