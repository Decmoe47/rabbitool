using System.ComponentModel.DataAnnotations;
using YamlDotNet.Serialization;

namespace Rabbitool.Conf;

public class Twitter
{
    [Required]
    public required int Interval { get; set; }

    [Required]
    public required string BearerToken { get; set; }

    [YamlMember(Alias = "x-csrf-token")]
    public string? XCsrfToken { get; set; }

    public string? Cookie { get; set; }
}
