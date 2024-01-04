using Autofac.Annotation;
using Autofac.Annotation.Condition;
using Rabbitool.Common.Constant;

namespace Rabbitool.Common.Configs;

[ConditionalOnProperty("youtube:enabled", "True")]
[PropertySource(Constants.ConfigFilename)]
[Component(AutofacScope = AutofacScope.SingleInstance)]
public class YoutubeConfig
{
    [Value("${notifier:enabled}")] public bool Enable { get; set; }

    [Value("${youtube:apiKey}")] public string ApiKey { get; set; } = null!;
}