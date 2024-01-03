using Autofac.Annotation;
using Rabbitool.Common.Constant;

namespace Rabbitool.Common.Configs;

[PropertySource(Constants.ConfigFilename)]
[Component]
public class QQBotConfig
{
    [Value("${qqBot.botQQ}")] public required string BotQQ { get; set; }

    [Value("${qqBot.appId}")] public required string AppId { get; set; }

    [Value("${qqBot.token}")] public required string Token { get; set; }

    [Value("${qqBot.secret}")] public required string Secret { get; set; }

    [Value("${qqBot.sandboxGuildName}")] public required string SandboxGuildName { get; set; }

    [Value("MarkdownTemplateIds", UseSpel = false, IgnoreUnresolvablePlaceholders = true)]
    public MarkdownTemplateIdsConfig? MarkdownTemplateIds { get; set; }
}