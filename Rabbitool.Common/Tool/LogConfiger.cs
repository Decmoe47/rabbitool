using Serilog;
using Serilog.Events;

namespace Rabbitool.Common.Tool;

public static class LogConfiger
{
    public static ILogger New(
        string consoleMinLevel = "verbose",
        string fileMinLevel = "verbose",
        string fileName = "rabbitool")
    {
        return new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console(
                restrictedToMinimumLevel: ConvertLevelFromString(consoleMinLevel),
                outputTemplate: "[{Timestamp:yyyy-MM-ddTHH:mm:sszzz} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.File(
                $"log/{fileName}.log",
                restrictedToMinimumLevel: ConvertLevelFromString(fileMinLevel),
                rollingInterval: RollingInterval.Day,
                rollOnFileSizeLimit: true,
                fileSizeLimitBytes: 1024 * 1024)
            .CreateLogger();
    }

    public static ILogger New(
        ErrorNotifierOptions errorNotifierOptions,
        string consoleMinLevel = "verbose",
        string fileMinLevel = "verbose",
        string fileName = "rabbitool")
    {
        return new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console(
                restrictedToMinimumLevel: ConvertLevelFromString(consoleMinLevel),
                outputTemplate: "[{Timestamp:yyyy-MM-ddTHH:mm:sszzz} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.File(
                $"log/{fileName}.log",
                restrictedToMinimumLevel: ConvertLevelFromString(fileMinLevel),
                rollingInterval: RollingInterval.Day,
                rollOnFileSizeLimit: true,
                fileSizeLimitBytes: 1024 * 1024)
            .WriteTo.Mail(errorNotifierOptions)
            .CreateLogger();
    }

    private static LogEventLevel ConvertLevelFromString(string level)
    {
        return level switch
        {
            "verbose" => LogEventLevel.Verbose,
            "debug" => LogEventLevel.Debug,
            "info" => LogEventLevel.Information,
            "warn" => LogEventLevel.Warning,
            "error" => LogEventLevel.Error,
            "fatal" => LogEventLevel.Fatal,
            _ => throw new ArgumentException($"Invaild level name {level}")
        };
    }
}
