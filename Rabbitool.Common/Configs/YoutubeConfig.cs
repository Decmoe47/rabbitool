using Autofac.Annotation;
using Autofac.Annotation.Condition;
using Rabbitool.Common.Constant;

namespace Rabbitool.Common.Configs;

[ConditionalOnProperty("youtube")]
[PropertySource(Constants.ConfigFilename)]
[Component]
public class YoutubeConfig
{
    [Value("${youtube:interval}")] public int Interval { get; set; }

    [Value("${youtube:apiKey}")] public string ApiKey { get; set; } = null!;
}