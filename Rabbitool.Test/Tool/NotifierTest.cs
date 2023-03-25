using Rabbitool.Config;

namespace Rabbitool.Common.Tool.Test;

public class ErrorNotifierTest
{
    private readonly Notifier _notifier;

    public ErrorNotifierTest()
    {
        Configs configs = Configs.Load("configs.yml");
        ErrorNotifierOptions opts = configs.Notifier!.ToOptions();
        opts.Interval = 1;
        opts.AllowedAmount = 6;

        _notifier = new Notifier(opts);
    }

    [Fact()]
    public async Task SendAsyncTestShouldReceivedAsync()
    {
        try
        {
            throw new ArgumentException("Test1 should be received.");
        }
        catch (ArgumentException ex)
        {
            for (int i = 0; i < 6; i++)
                await _notifier.SendAsync(ex);
        }
    }

    [Fact()]
    public async Task SendAsyncTestShouldNotReceivedAsync()
    {
        try
        {
            throw new ArgumentException("Test2 shouldn't be received.");
        }
        catch (ArgumentException ex)
        {
            for (int i = 0; i < 5; i++)
                await _notifier.SendAsync(ex);
        }
    }
}
