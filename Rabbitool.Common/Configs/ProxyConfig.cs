using Autofac.Annotation;
using Autofac.Annotation.Condition;
using Rabbitool.Common.Constant;

namespace Rabbitool.Common.Configs;

[ConditionalOnProperty("proxy")]
[PropertySource(Constants.ConfigFilename)]
[Component]
public class ProxyConfig
{
    [Value("${proxy:http}")] public string Http { get; set; } = null!;

    [Value("${proxy:https}")] public string Https { get; set; } = null!;
}