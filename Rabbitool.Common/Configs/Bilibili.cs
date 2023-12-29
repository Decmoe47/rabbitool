using System.ComponentModel.DataAnnotations;

namespace Rabbitool.Common.Configs;

public class Bilibili
{
    [Required] public int Interval { get; init; }
}