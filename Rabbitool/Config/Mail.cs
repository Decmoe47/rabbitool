using System.ComponentModel.DataAnnotations;

namespace Rabbitool.Conf;

public class Mail
{
    [Required]
    public int Interval { get; set; }
}
