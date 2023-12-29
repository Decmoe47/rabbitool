using System.ComponentModel.DataAnnotations;
using Rabbitool.Common.Tool;

namespace Rabbitool.Configs;

public class Notifier
{
    [Required] public required string Host { get; set; }

    [Required] public int Port { get; set; }

    [Required] public bool Ssl { get; set; }

    [Required] public required string UserName { get; set; }

    [Required] public required string Password { get; set; }

    [Required] public required string From { get; set; }

    [Required] public required string[] To { get; set; }

    [Required] public int Interval { get; set; }

    [Required] public int AllowedAmount { get; set; }

    [Required] public int Timeout { get; set; }

    public ErrorNotifierOptions ToOptions()
    {
        return new ErrorNotifierOptions
        {
            Host = Host,
            Port = Port,
            Ssl = Ssl,
            Username = UserName,
            Password = Password,
            From = From,
            To = To,
            Interval = Interval,
            AllowedAmount = AllowedAmount,
            Timeout = Timeout
        };
    }
}