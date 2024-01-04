using Autofac.Annotation;
using Autofac.Annotation.Condition;
using Rabbitool.Common.Constant;

namespace Rabbitool.Common.Configs;

[ConditionalOnProperty("mail:enabled", "True")]
[PropertySource(Constants.ConfigFilename)]
[Component(AutofacScope = AutofacScope.SingleInstance)]
public class MailConfig
{
    [Value("${mail:enabled}")] public bool Enable { get; set; }
}