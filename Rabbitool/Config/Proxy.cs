using System.ComponentModel.DataAnnotations;

namespace Rabbitool.Config;

public class Proxy
{
    [Required]
    public string HttpProxy { get; set; } = string.Empty;

    [Required]
    public string HttpsProxy { get; set; } = string.Empty;
}
