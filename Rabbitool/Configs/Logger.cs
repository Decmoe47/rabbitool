using System.ComponentModel.DataAnnotations;

namespace Rabbitool.Configs;

public class Logger
{
    [Required] public required string ConsoleLevel { get; set; }

    [Required] public required string FileLevel { get; set; }
}