using Serilog;

namespace Rabbitool.Common.Tool.Test;

public class LogConfigTest
{
    public LogConfigTest()
    {
        Configs configs = Configs.Load("configs.yml");
        ErrorNotifierOptions opts = configs.ErrorNotifier!.ToOptions();
        opts.RefreshMinutes = 1;
        opts.MaxAmount = 6;
        opts.AllowedAmount = 6;
        LogConfig.Register(opts);
    }

    [Fact()]
    public void LogToEmailShouldReceivedTest()
    {
        try
        {
            throw new ArgumentException("Test1 should be received.");
        }
        catch (ArgumentException ex)
        {
            for (int i = 0; i < 6; i++)
                Log.Error(ex, ex.Message);
        }
    }

    [Fact()]
    public void LogToEmailShouldNotReceivedTest()
    {
        try
        {
            throw new ArgumentException("Test2 shouldn't be received.");
        }
        catch (ArgumentException ex)
        {
            for (int i = 0; i < 5; i++)
                Log.Error(ex, ex.Message);
        }
    }
}
