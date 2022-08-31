namespace Rabbitool.Common.Tool.Test;

public class ErrorNotifierTest
{
    private readonly ErrorNotifier _notifier;

    public ErrorNotifierTest()
    {
        Configs configs = Configs.Load("configs.yml");
        ErrorNotifierOptions opts = configs.ErrorNotifier!.ToOptions();
        opts.RefreshMinutes = 1;
        opts.MaxAmount = 6;
        opts.AllowedAmount = 6;

        _notifier = new ErrorNotifier(opts);
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
