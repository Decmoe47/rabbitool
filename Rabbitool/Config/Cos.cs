using System.ComponentModel.DataAnnotations;

namespace Rabbitool.Config;

public class Cos
{
    [Required]
    public string SecretId { get; set; } = null!;

    [Required]
    public string SecretKey { get; set; } = null!;

    [Required]
    public string BucketName { get; set; } = null!;

    [Required]
    public string Region { get; set; } = null!;
}
