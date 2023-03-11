using YamlDotNet.Serialization;

namespace Rabbitool.Config;

public class Twitter
{
    [YamlMember(Alias = "x_csrf_token")]
    public string? XCsrfToken { get; set; }

    public string? Cookie { get; set; }
    public string? ApiV2Token { get; set; }
}
