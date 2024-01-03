using Autofac.Annotation;
using Autofac.Annotation.Condition;
using Rabbitool.Common.Constant;

namespace Rabbitool.Common.Configs;

[ConditionalOnProperty("proxy")]
[PropertySource(Constants.ConfigFilename)]
[Component]
public class ProxyConfig
{
    [Value("${proxy.http}")] public required string Http { get; set; }

    [Value("${proxy.https}")] public required string Https { get; set; }
}