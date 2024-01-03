using Autofac.Annotation;
using Autofac.Annotation.Condition;
using Rabbitool.Common.Constant;

namespace Rabbitool.Common.Configs;

[ConditionalOnProperty("mail")]
[PropertySource(Constants.ConfigFilename)]
[Component]
public class MailConfig
{
    [Value("${mail:interval}")] public int Interval { get; set; }
}