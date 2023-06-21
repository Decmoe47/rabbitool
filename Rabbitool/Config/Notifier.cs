using System.ComponentModel.DataAnnotations;

namespace Rabbitool.Conf;

public class Notifier
{
    [Required]
    public required string Host { get; set; }

    [Required]
    public required int Port { get; set; }

    [Required]
    public required bool Ssl { get; set; }

    [Required]
    public required string UserName { get; set; }

    [Required]
    public required string Password { get; set; }

    [Required]
    public required string From { get; set; }

    [Required]
    public required string[] To { get; set; }

    [Required]
    public required int Interval { get; set; }

    [Required]
    public required int AllowedAmount { get; set; }

    public Common.Tool.ErrorNotifierOptions ToOptions()
    {
        return new Common.Tool.ErrorNotifierOptions()
        {
            Host = Host,
            Port = Port,
            Ssl = Ssl,
            Username = UserName,
            Password = Password,
            From = From,
            To = To,
            Interval = Interval,
            AllowedAmount = AllowedAmount
        };
    }
}
