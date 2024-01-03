using Autofac.Annotation;
using Rabbitool.Common.Constant;

namespace Rabbitool.Common.Configs;

[PropertySource(Constants.ConfigFilename)]
[Component]
public class LoggerConfig
{
    [Value("${logger:consoleLevel}")] public string ConsoleLevel { get; set; } = null!;

    [Value("${logger:fileLevel}")] public string FileLevel { get; set; } = null!;

    [Value("${logger:filename}", IgnoreUnresolvablePlaceholders = true)]
    public string Filename { get; set; } = "rabbitool";
}