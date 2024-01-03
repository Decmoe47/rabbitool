using System.Diagnostics;
using Rabbitool.Common.Util;
using Xunit.Abstractions;
using Timer = System.Timers.Timer;

namespace Rabbitool.Test.Util;

public class LimiterUtilTest(ITestOutputHelper output)
{
    private readonly LimiterUtil _limiter = new(1, 1);

    [Fact]
    public async Task WaitTestAsync()
    {
        int count = await CountAsync("0", _limiter);
        Assert.Equal(10, count);
    }

    [Fact]
    public async Task WaitForMutiThreadsTestAsync()
    {
        List<Task<int>> tasks = [];
        Stopwatch watch = new();

        for (int i = 0; i < 2; i++)
            tasks.Add(CountAsync(i.ToString(), _limiter));

        watch.Start();
        int[] results = await Task.WhenAll(tasks);
        watch.Stop();

        Assert.Equal(10 * 2, results.Sum());
        Assert.Equal(TimeSpan.FromSeconds(10), watch.Elapsed);
    }

    private Task<int> CountAsync(string name, LimiterUtil limiter)
    {
        bool done = false;
        int count = 0;

        Timer t = new(TimeSpan.FromSeconds(10).TotalMilliseconds);
        t.Elapsed += (sender, e) => done = true;
        t.AutoReset = false;
        t.Enabled = true;

        while (!done)
        {
            limiter.Wait();
            count++;
            output.WriteLine($"Now count is {count}");
        }

        return Task.FromResult(count);
    }
}