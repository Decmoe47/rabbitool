using System.ComponentModel.DataAnnotations;
using YamlDotNet.Serialization;

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

    public Logger? Logger { get; set; }
}

