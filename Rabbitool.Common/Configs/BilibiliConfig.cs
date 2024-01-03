using Autofac.Annotation;
using Autofac.Annotation.Condition;
using Rabbitool.Common.Constant;

namespace Rabbitool.Common.Configs;

[ConditionalOnProperty("bilibili")]
[PropertySource(Constants.ConfigFilename)]
[Component]
public class BilibiliConfig
{
    [Value("${bilibili.interval}")] public int Interval { get; set; }
}