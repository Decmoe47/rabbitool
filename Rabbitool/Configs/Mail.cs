using System.ComponentModel.DataAnnotations;

namespace Rabbitool.Configs;

public class Mail
{
    [Required] public int Interval { get; set; }
}