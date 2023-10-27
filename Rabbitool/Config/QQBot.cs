using System.ComponentModel.DataAnnotations;

namespace Rabbitool.Conf;

public class QQBot
{
    [Required]
    public required string AppId { get; set; }

    [Required]
    public required string Token { get; set; }

    [Required]
    public required string SandboxGuildName { get; set; }

    public MarkdownTemplateIds? MarkdownTemplateIds { get; set; }
}

