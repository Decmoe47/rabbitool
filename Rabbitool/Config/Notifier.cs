using System.ComponentModel.DataAnnotations;

namespace Rabbitool.Config;

public class Notifier
{
    [Required]
    public string SenderHost { get; set; } = null!;

    [Required]
    public int SenderPort { get; set; }

    [Required]
    public bool UsingSsl { get; set; }

    [Required]
    public string SenderUsername { get; set; } = null!;

    [Required]
    public string SenderPassword { get; set; } = null!;

    [Required]
    public string SenderAddress { get; set; } = null!;

    [Required]
    public string[] ReceiverAddresses { get; set; } = null!;

    [Required]
    public int IntervalMinutes { get; set; }

    [Required]
    public int AllowedAmount { get; set; }

    public Common.Tool.ErrorNotifierOptions ToOptions()
    {
        return new Common.Tool.ErrorNotifierOptions()
        {
            Host = SenderHost,
            Port = SenderPort,
            Ssl = UsingSsl,
            Username = SenderUsername,
            Password = SenderPassword,
            From = SenderAddress,
            To = ReceiverAddresses,
            IntervalMinutes = IntervalMinutes,
            AllowedAmount = AllowedAmount
        };
    }
}
