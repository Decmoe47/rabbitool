using System.ComponentModel.DataAnnotations;

namespace Rabbitool.Config;

public class Log
{
    [Required]
    public string ConsoleLevel { get; set; } = null!;

    [Required]
    public string FileLevel { get; set; } = null!;
}
