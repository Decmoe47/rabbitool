using System.ComponentModel.DataAnnotations;

namespace Rabbitool.Config;

public class Mail
{
    [Required]
    public int Interval { get; set; }
}
