using System.ComponentModel.DataAnnotations;

namespace Rabbitool.Common.Configs;

public class Proxy
{
    [Required] public required string Http { get; init; }

    [Required] public required string Https { get; init; }
}