namespace Rabbitool.Common.Util;

public class LimiterUtil
{
    private readonly float _rate;
    private readonly int _capacity;
    private float _currentAmount;
    private long _lastTriedTime;

    /// <param name="rate">每秒增加多少</param>
    /// <param name="capacity">桶总量</param>
    public LimiterUtil(float rate, int capacity)
    {
        _rate = rate;
        _capacity = capacity;
        _currentAmount = 0;
        _lastTriedTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }

    public bool Allow(int consume = 1)
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        float increment = (now - _lastTriedTime) * _rate;
        _currentAmount = Math.Min(increment + _currentAmount, _capacity);
        _lastTriedTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        if (consume > _currentAmount)
            return false;

        _currentAmount -= consume;
        return true;
    }

    public void Wait(int consume = 1)
    {
        while (!Allow(consume))
            Thread.Sleep(1000);
    }
}
