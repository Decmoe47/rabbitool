using System.ComponentModel.DataAnnotations;

namespace Rabbitool.Conf;

public class Bilibili
{
    [Required]
    public int Interval { get; set; }
}
