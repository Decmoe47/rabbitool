using Autofac.Annotation;
using Autofac.Annotation.Condition;
using Rabbitool.Common.Constant;

namespace Rabbitool.Common.Configs;

[ConditionalOnProperty("twitter")]
[PropertySource(Constants.ConfigFilename)]
[Component]
public class TwitterConfig
{
    [Value("${twitter.interval}")] public required int Interval { get; set; }

    [Value("${twitter.bearerToken}", IgnoreUnresolvablePlaceholders = true)]
    public string? BearerToken { get; set; }

    [Value("${twitter.x-csrf-token}")] public string? XCsrfToken { get; set; }

    [Value("${twitter.cookie}")] public string? Cookie { get; set; }
}