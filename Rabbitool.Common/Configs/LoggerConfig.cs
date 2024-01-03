using Autofac.Annotation;
using Rabbitool.Common.Constant;

namespace Rabbitool.Common.Configs;

[PropertySource(Constants.ConfigFilename)]
[Component]
public class LoggerConfig
{
    [Value("${logger.consoleLevel}")] public required string ConsoleLevel { get; set; }

    [Value("${logger.fileLevel}")] public required string FileLevel { get; set; }

    [Value("${logger.filename}", IgnoreUnresolvablePlaceholders = true)]
    public string Filename { get; set; } = "rabbitool";
}