using System.ComponentModel.DataAnnotations;
using YamlDotNet.Serialization;

namespace Rabbitool.Config;

public class QQBot
{
    [Required]
    public string AppId { get; set; } = null!;

    [Required]
    public string Token { get; set; } = null!;

    [Required]
    [YamlMember(Alias = "isSandbox")]
    public bool IsSandBox { get; set; }

    [Required]
    public string SandboxGuildName { get; set; } = null!;
}
