using System.ComponentModel.DataAnnotations;

namespace Rabbitool.Conf;

public class Proxy
{
    [Required]
    public required string Http { get; set; }

    [Required]
    public required string Https { get; set; }
}
