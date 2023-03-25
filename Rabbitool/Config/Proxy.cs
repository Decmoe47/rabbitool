using System.ComponentModel.DataAnnotations;

namespace Rabbitool.Config;

public class Proxy
{
    [Required]
    public string Http { get; set; } = string.Empty;

    [Required]
    public string Https { get; set; } = string.Empty;
}
