using Autofac.Annotation;
using Rabbitool.Common.Constant;

namespace Rabbitool.Common.Configs;

[PropertySource(Constants.ConfigFilename)]
[Component]
public class QQBotConfig
{
    [Value("${qqBot:botQQ}")] public string BotQQ { get; set; } = null!;

    [Value("${qqBot:appId}")] public string AppId { get; set; } = null!;

    [Value("${qqBot:token}")] public string Token { get; set; } = null!;

    [Value("${qqBot:secret}")] public string Secret { get; set; } = null!;

    [Value("${qqBot:sandboxGuildName}")] public string SandboxGuildName { get; set; } = null!;

    [Value("qqBot:markdownTemplateIds", UseSpel = false, IgnoreUnresolvablePlaceholders = true)]
    public MarkdownTemplateIdsConfig? MarkdownTemplateIds { get; set; }
}