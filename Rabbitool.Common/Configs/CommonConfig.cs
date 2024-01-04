using Autofac.Annotation;
using Rabbitool.Common.Constant;

namespace Rabbitool.Common.Configs;

[PropertySource(Constants.ConfigFilename)]
[Component(AutofacScope = AutofacScope.SingleInstance)]
public class CommonConfig
{
    [Value("${redirectUrl}")] public string RedirectUrl { get; set; } = null!;

    [Value("${userAgent}")] public string UserAgent { get; set; } = null!;

    [Value("${inTestEnvironment}", IgnoreUnresolvablePlaceholders = true)]
    public bool InTestEnvironment { get; set; }

    [Value("${dbPath}")] public string DbPath { get; set; } = null!;

    [Value("proxy", UseSpel = false, IgnoreUnresolvablePlaceholders = true)]
    public ProxyConfig? Proxy { get; set; }
}