using System.ComponentModel.DataAnnotations;

namespace Rabbitool.Common.Configs;

public class Logger
{
    [Required] public required string ConsoleLevel { get; init; }

    [Required] public required string FileLevel { get; init; }
}