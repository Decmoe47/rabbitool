namespace Rabbitool.Service.Test;

public class BilibiliServiceTest
{
    private static readonly List<uint> _testUids = new() { 2920960, 4415701, 6697975, 488976342, 1871001 };
    private readonly BilibiliService _svc;

    public BilibiliServiceTest()
    {
        _svc = new BilibiliService("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/102.0.5005.124 Safari/537.36 Edg/102.0.1245.44");
    }

    [Theory()]
    [InlineData(2920960)]
    [InlineData(4415701)]
    [InlineData(6697975)]
    [InlineData(488976342)]
    [InlineData(1871001)]
    public async Task GetLatestDynamicAsyncTestAsync(uint uid)
    {
        await _svc.GetLatestDynamicAsync(uid);
    }

    [Theory()]
    [InlineData(2920960)]
    [InlineData(4415701)]
    [InlineData(6697975)]
    [InlineData(488976342)]
    [InlineData(1871001)]
    public async Task GetGetLiveAsyncTestAsync(uint uid)
    {
        await _svc.GetLiveAsync(uid);
    }
}
