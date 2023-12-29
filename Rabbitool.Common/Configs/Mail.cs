using System.ComponentModel.DataAnnotations;

namespace Rabbitool.Common.Configs;

public class Mail
{
    [Required] public int Interval { get; init; }
}