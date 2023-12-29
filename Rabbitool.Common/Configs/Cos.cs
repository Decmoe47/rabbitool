using System.ComponentModel.DataAnnotations;

namespace Rabbitool.Common.Configs;

public class Cos
{
    [Required] public required string SecretId { get; init; }

    [Required] public required string SecretKey { get; init; }

    [Required] public required string BucketName { get; init; }

    [Required] public required string Region { get; init; }
}