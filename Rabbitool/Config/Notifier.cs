using System.ComponentModel.DataAnnotations;

namespace Rabbitool.Config;

public class Notifier
{
    [Required]
    public string Host { get; set; } = null!;

    [Required]
    public int Port { get; set; }

    [Required]
    public bool Ssl { get; set; }

    [Required]
    public string Username { get; set; } = null!;

    [Required]
    public string Password { get; set; } = null!;

    [Required]
    public string From { get; set; } = null!;

    [Required]
    public string[] To { get; set; } = null!;

    [Required]
    public int Interval { get; set; }

    [Required]
    public int AllowedAmount { get; set; }

    public Common.Tool.ErrorNotifierOptions ToOptions()
    {
        return new Common.Tool.ErrorNotifierOptions()
        {
            Host = Host,
            Port = Port,
            Ssl = Ssl,
            Username = Username,
            Password = Password,
            From = From,
            To = To,
            Interval = Interval,
            AllowedAmount = AllowedAmount
        };
    }
}
