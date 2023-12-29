using System.ComponentModel.DataAnnotations;
using Rabbitool.Common.Tool;

namespace Rabbitool.Common.Configs;

public class Notifier
{
    [Required] public required string Host { get; init; }

    [Required] public int Port { get; init; }

    [Required] public bool Ssl { get; init; }

    [Required] public required string UserName { get; init; }

    [Required] public required string Password { get; init; }

    [Required] public required string From { get; init; }

    [Required] public required string[] To { get; init; }

    [Required] public int Interval { get; init; }

    [Required] public int AllowedAmount { get; init; }

    [Required] public int Timeout { get; init; }

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