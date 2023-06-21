using System.ComponentModel.DataAnnotations;

namespace Rabbitool.Conf;

public class Logger
{
    [Required]
    public required string ConsoleLevel { get; set; }

    [Required]
    public required string FileLevel { get; set; }
}
