namespace Rabbitool.Common.Util;

public class LimiterUtil
{
    private readonly int _rate, _capacity;
    private long _currentAmount, _lastConsumeTime;

    public LimiterUtil(int rate, int capacity)
    {
        _rate = rate;
        _capacity = capacity;
        _currentAmount = capacity;
        _lastConsumeTime = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds();
    }

    public bool Allow(int consume = 1)
    {
        long now = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds();
        long increment = (now - _lastConsumeTime) * _rate;
        _currentAmount = Math.Min(increment + _currentAmount, _capacity);
        if (consume > _currentAmount)
            return false;
        _lastConsumeTime = now;
        _currentAmount -= 1;
        return true;
    }

    public void Wait(int consume = 1)
    {
        while (!Allow(consume))
            Thread.Sleep(1000);
    }
}
