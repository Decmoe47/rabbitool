using Serilog;
using Serilog.Formatting.Compact;

namespace Rabbitool.Common.Tool;

public class LogConfig
{
    public static void Register()
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .WriteTo.File(
                new CompactJsonFormatter(),
                "log/rabbitool.log",
                rollOnFileSizeLimit: true,
                fileSizeLimitBytes: 512)
            .CreateLogger();
        AppDomain.CurrentDomain.ProcessExit += (sender, e) => Log.CloseAndFlush();
    }
}
