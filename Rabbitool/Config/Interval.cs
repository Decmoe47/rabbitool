using System.ComponentModel.DataAnnotations;

namespace Rabbitool.Config;

public class Interval
{
    [Required]
    public int BilibiliPlugin { get; set; }

    [Required]
    public int YoutubePlugin { get; set; }

    [Required]
    public int TwitterPlugin { get; set; }

    [Required]
    public int MailPlugin { get; set; }
}
