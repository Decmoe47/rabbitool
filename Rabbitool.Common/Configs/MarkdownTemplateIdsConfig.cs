using Autofac.Annotation;
using Autofac.Annotation.Condition;
using Rabbitool.Common.Constant;

namespace Rabbitool.Common.Configs;

[ConditionalOnProperty("qqBot.markdownTemplateIds")]
[PropertySource(Constants.ConfigFilename)]
[Component]
public class MarkdownTemplateIdsConfig
{
    [Value("${qqBot.markdownTemplateIds.textOnly}")]
    public required string TextOnly { get; set; }

    [Value("${qqBot.markdownTemplateIds.withImage}")]
    public required string WithImage { get; set; }

    [Value("${qqBot.markdownTemplateIds.containsOriginTextOnly}")]
    public required string ContainsOriginTextOnly { get; set; }

    [Value("${qqBot.markdownTemplateIds.containsOriginWithImage}")]
    public required string ContainsOriginWithImage { get; set; }
}