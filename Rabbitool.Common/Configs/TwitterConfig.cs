using Autofac.Annotation;
using Autofac.Annotation.Condition;
using Rabbitool.Common.Constant;

namespace Rabbitool.Common.Configs;

[ConditionalOnProperty("twitter:enabled", "True")]
[PropertySource(Constants.ConfigFilename)]
[Component]
public class TwitterConfig
{
    [Value("${notifier:enabled}")] public bool Enable { get; set; }

    [Value("${twitter:bearerToken}", IgnoreUnresolvablePlaceholders = true)]
    public string? BearerToken { get; set; }

    [Value("${twitter:x-csrf-token}")] public string? XCsrfToken { get; set; }

    [Value("${twitter:cookie}")] public string? Cookie { get; set; }
}