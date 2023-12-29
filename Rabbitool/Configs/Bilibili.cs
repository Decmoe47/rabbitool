using System.ComponentModel.DataAnnotations;

namespace Rabbitool.Configs;

public class Bilibili
{
    [Required] public int Interval { get; set; }
}