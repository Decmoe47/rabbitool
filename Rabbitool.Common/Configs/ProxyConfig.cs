using Autofac.Annotation;
using Autofac.Annotation.Condition;
using Rabbitool.Common.Constant;

namespace Rabbitool.Common.Configs;

[ConditionalOnProperty("proxy:enabled", "True")]
[PropertySource(Constants.ConfigFilename)]
[Component(AutofacScope = AutofacScope.SingleInstance)]
public class ProxyConfig
{
    [Value("${notifier:enabled}")] public bool Enable { get; set; }

    [Value("${proxy:http}")] public string Http { get; set; } = null!;

    [Value("${proxy:https}")] public string Https { get; set; } = null!;
}