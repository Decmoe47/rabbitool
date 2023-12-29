using System.ComponentModel.DataAnnotations;

namespace Rabbitool.Configs;

public class Youtube
{
    [Required] public required int Interval { get; set; }

    [Required] public required string ApiKey { get; set; }
}