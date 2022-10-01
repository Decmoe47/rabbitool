using Serilog;
using Serilog.Events;

namespace Rabbitool.Common.Tool;

public static class LogConfig
{
    public static void Register(string consoleMinLevel = "verbose", string fileMinLevel = "verbose")
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console(
                restrictedToMinimumLevel: ConvertLevelFromString(consoleMinLevel),
                outputTemplate: "[{Timestamp:yyyy-MM-ddTHH:mm:sszzz} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.File(
                $"log/rabbitool_{DateTime.Now:yyyyMMdd_HHmmsszz}.log",
                restrictedToMinimumLevel: ConvertLevelFromString(fileMinLevel),
                rollOnFileSizeLimit: true,
                fileSizeLimitBytes: 1024 * 1024)
            .CreateLogger();
        Console.CancelKeyPress += (sender, e) => Log.CloseAndFlush();
    }

    public static void Register(
        ErrorNotifierOptions errorNotifierOptions,
        string consoleMinLevel = "verbose",
        string fileMinLevel = "verbose")
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console(
                restrictedToMinimumLevel: ConvertLevelFromString(consoleMinLevel),
                outputTemplate: "[{Timestamp:yyyy-MM-ddTHH:mm:sszzz} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.File(
                $"log/rabbitool_{DateTime.Now:yyyyMMdd_HHmmsszz}.log",
                restrictedToMinimumLevel: ConvertLevelFromString(fileMinLevel),
                rollOnFileSizeLimit: true,
                fileSizeLimitBytes: 1024 * 1024)
            .WriteTo.Mail(errorNotifierOptions)
            .CreateLogger();
        Console.CancelKeyPress += (sender, e) => Log.CloseAndFlush();
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
