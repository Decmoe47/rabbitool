using Autofac.Annotation;
using Autofac.Annotation.Condition;
using Rabbitool.Common.Constant;

namespace Rabbitool.Common.Configs;

[ConditionalOnProperty("qqBot:markdownTemplateIds:textOnly")]
[PropertySource(Constants.ConfigFilename)]
[Component(AutofacScope = AutofacScope.SingleInstance)]
public class MarkdownTemplateIdsConfig
{
    [Value("${qqBot:markdownTemplateIds:textOnly}")]
    public string TextOnly { get; set; } = null!;

    [Value("${qqBot:markdownTemplateIds:withImage}")]
    public string WithImage { get; set; } = null!;

    [Value("${qqBot:markdownTemplateIds:containsOriginTextOnly}")]
    public string ContainsOriginTextOnly { get; set; } = null!;

    [Value("${qqBot:markdownTemplateIds:containsOriginWithImage}")]
    public string ContainsOriginWithImage { get; set; } = null!;
}