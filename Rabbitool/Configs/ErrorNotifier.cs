using System.ComponentModel.DataAnnotations;

namespace Rabbitool.Config;

public class ErrorNotifier
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
    public int RefreshMinutes { get; set; }

    [Required]
    public int MaxAmount { get; set; }

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
            RefreshMinutes = RefreshMinutes,
            MaxAmount = MaxAmount,
            AllowedAmount = AllowedAmount
        };
    }
}
