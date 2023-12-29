using Rabbitool.Configs;

namespace Rabbitool.Common.Tool.Test;

public class ErrorNotifierTest
{
    private readonly Notifier _notifier;

    public ErrorNotifierTest()
    {
        Env env = Env.Load("configs.yml");
        ErrorNotifierOptions opts = env.Notifier!.ToOptions();
        opts.Interval = 1;
        opts.AllowedAmount = 6;

        _notifier = new Notifier(opts);
    }

    [Fact()]
    public void SendTestShouldReceived()
    {
        try
        {
            throw new ArgumentException("Test1 should be received.");
        }
        catch (ArgumentException ex)
        {
            for (int i = 0; i < 10; i++)
                _notifier.Notify(ex.ToString(), ex.ToString());
            Thread.Sleep(TimeSpan.FromMinutes(7));
            _notifier.Notify("recovered", "recovered");
        }
    }

    [Fact()]
    public void SendAsyncTestShouldNotReceived()
    {
        try
        {
            throw new ArgumentException("Test2 shouldn't be received.");
        }
        catch (ArgumentException ex)
        {
            for (int i = 0; i < 5; i++)
                _notifier.Notify(ex.ToString(), ex.ToString());
        }
    }
}
