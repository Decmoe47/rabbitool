using System.ComponentModel.DataAnnotations;

namespace Rabbitool.Config;

public class Bilibili
{
    [Required]
    public int Interval { get; set; }
}
