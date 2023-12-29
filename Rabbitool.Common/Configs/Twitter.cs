using System.ComponentModel.DataAnnotations;
using YamlDotNet.Serialization;

namespace Rabbitool.Common.Configs;

public class Twitter
{
    [Required] public required int Interval { get; init; }

    [Required] public required string BearerToken { get; init; }

    [YamlMember(Alias = "x-csrf-token")] public string? XCsrfToken { get; init; }

    public string? Cookie { get; init; }
}