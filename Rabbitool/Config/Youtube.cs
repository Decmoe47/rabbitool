using System.ComponentModel.DataAnnotations;

namespace Rabbitool.Conf;

public class Youtube
{
    [Required]
    public required int Interval { get; set; }

    [Required]
    public required string ApiKey { get; set; }
}
