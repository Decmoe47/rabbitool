using Rabbitool.Common.Configs;
using Serilog;
using Serilog.Events;

namespace Rabbitool.Common.Provider;

public static class LoggerConfigurationProvider
{
    public static ILogger GetConfiguration(
        LoggerConfig loggerConfig)
    {
        return new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console(
                ConvertLevelFromString(loggerConfig.ConsoleLevel),
                "[{Timestamp:yyyy-MM-ddTHH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.File(
                $"logs/{loggerConfig.Filename}.log",
                ConvertLevelFromString(loggerConfig.FileLevel),
                rollingInterval: RollingInterval.Day,
                rollOnFileSizeLimit: true,
                fileSizeLimitBytes: 1024 * 1024)
            .CreateLogger();
    }

    public static ILogger GetConfiguration(
        LoggerConfig loggerConfig, NotifierConfig notifierConfig)
    {
        return new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console(
                ConvertLevelFromString(loggerConfig.ConsoleLevel),
                "[{Timestamp:yyyy-MM-ddTHH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.File(
                $"logs/{loggerConfig.Filename}.log",
                ConvertLevelFromString(loggerConfig.FileLevel),
                rollingInterval: RollingInterval.Day,
                rollOnFileSizeLimit: true,
                fileSizeLimitBytes: 1024 * 1024)
            .WriteTo.Mail(notifierConfig.ToOptions())
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
            _ => throw new ArgumentException($"Invalid level name {level}")
        };
    }
}