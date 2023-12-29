using System.ComponentModel.DataAnnotations;

namespace Rabbitool.Common.Configs;

public class QQBot
{
    [Required] public required string BotQQ { get; init; }

    [Required] public required string AppId { get; init; }

    [Required] public required string Token { get; init; }

    [Required] public required string Secret { get; init; }

    [Required] public required string SandboxGuildName { get; init; }

    public MarkdownTemplateIds? MarkdownTemplateIds { get; init; }
}