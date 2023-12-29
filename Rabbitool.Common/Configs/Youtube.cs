using System.ComponentModel.DataAnnotations;

namespace Rabbitool.Common.Configs;

public class Youtube
{
    [Required] public required int Interval { get; init; }

    [Required] public required string ApiKey { get; init; }
}