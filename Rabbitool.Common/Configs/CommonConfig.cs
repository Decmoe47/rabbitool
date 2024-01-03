using Autofac.Annotation;
using Rabbitool.Common.Constant;

namespace Rabbitool.Common.Configs;

[PropertySource(Constants.ConfigFilename)]
[Component]
public class CommonConfig
{
    [Value("${redirectUrl}")] public required string RedirectUrl { get; set; }

    [Value("${userAgent}")] public required string UserAgent { get; set; }

    [Value("${inTestEnvironment}", IgnoreUnresolvablePlaceholders = true)]
    public bool InTestEnvironment { get; set; }

    [Value("${dbPath}")] public required string DbPath { get; set; }

    [Value("proxy", UseSpel = false, IgnoreUnresolvablePlaceholders = true)]
    public ProxyConfig? Proxy { get; set; }
}