using System.ComponentModel.DataAnnotations;

namespace Rabbitool.Configs;

public class Cos
{
    [Required] public required string SecretId { get; set; }

    [Required] public required string SecretKey { get; set; }

    [Required] public required string BucketName { get; set; }

    [Required] public required string Region { get; set; }
}