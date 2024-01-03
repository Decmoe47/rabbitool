using Rabbitool.Api;

namespace Rabbitool.Test.Service;

public class BilibiliApiTest(BilibiliApi api) : BaseTest
{
    private static readonly List<uint> TestUids = [2920960, 4415701, 6697975, 488976342, 1871001];
    
    [Theory]
    [InlineData(2920960)]
    [InlineData(4415701)]
    [InlineData(6697975)]
    [InlineData(488976342)]
    [InlineData(1871001)]
    public async Task GetLatestDynamicAsyncTestAsync(uint uid)
    {
        await api.GetLatestDynamicAsync(uid);
    }

    [Theory]
    [InlineData(2920960)]
    [InlineData(4415701)]
    [InlineData(6697975)]
    [InlineData(488976342)]
    [InlineData(1871001)]
    public async Task GetGetLiveAsyncTestAsync(uint uid)
    {
        await api.GetLiveAsync(uid);
    }
}