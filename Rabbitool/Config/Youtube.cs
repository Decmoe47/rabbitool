using System.ComponentModel.DataAnnotations;

namespace Rabbitool.Config;

public class Youtube
{
    [Required]
    public int Interval { get; set; }

    [Required]
    public string ApiKey { get; set; } = null!;
}
