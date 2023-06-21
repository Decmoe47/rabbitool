using Rabbitool.Conf;
using Log = Serilog.Log;

namespace Rabbitool.Common.Tool.Test;

public class LogConfigTest
{
    public LogConfigTest()
    {
        Configs configs = Configs.Load("configs.yml");
        ErrorNotifierOptions opts = configs.Notifier!.ToOptions();
        opts.Interval = 60;
        opts.AllowedAmount = 25;
        Log.Logger = LogConfiger.New(opts);
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
            for (int i = 0; i < 26; i++)
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
            for (int i = 0; i < 24; i++)
                Log.Error(ex, ex.Message);
        }
    }
}
