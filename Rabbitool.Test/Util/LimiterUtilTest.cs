using Xunit.Abstractions;

namespace Rabbitool.Common.Util.Test;

public class LimiterUtilTest
{
    private readonly LimiterUtil _limiter;
    private readonly ITestOutputHelper _output;

    public LimiterUtilTest(ITestOutputHelper output)
    {
        _limiter = new LimiterUtil(1, 1);
        _output = output;
    }

    [Fact()]
    public async Task WaitTestAsync()
    {
        int count = await CountAsync("0", _limiter);

        Assert.Equal(10, count);
    }

    [Fact()]
    public async Task WaitForMutiThreadsTestAsync()
    {
        List<Task<int>> tasks = new();

        System.Diagnostics.Stopwatch watch = new();

        for (int i = 0; i < 2; i++)
            tasks.Add(CountAsync(i.ToString(), _limiter));

        watch.Start();
        int[] results = await Task.WhenAll(tasks);
        watch.Stop();

        Assert.Equal(10 * 2, results.Sum());
        Assert.Equal(TimeSpan.FromSeconds(10), watch.Elapsed);
    }

    public async Task<int> CountAsync(string name, LimiterUtil limiter)
    {
        bool done = false;
        int count = 0;

        System.Timers.Timer t = new(TimeSpan.FromSeconds(10).TotalMilliseconds);
        t.Elapsed += (sender, e) => done = true;
        t.AutoReset = false;
        t.Enabled = true;

        while (!done)
        {
            limiter.Wait();
            count++;
            _output.WriteLine($"Now count is {count}");
        }

        return count;
    }
}
