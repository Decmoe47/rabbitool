using Rabbitool.Api;

namespace Rabbitool.Test.Service;

public class YoutubeApiTest(YoutubeApi api) : BaseTest
{
    [Theory]
    [InlineData("UCt0clH12Xk1-Ej5PXKGfdPA")]
    [InlineData("UCTkyJbRhal4voLZxmdRSssQ")]
    [InlineData("UCkIimWZ9gBJRamKF0rmPU8w")]
    public async Task GetLatestVideoOrLiveAsyncTestAsync(string channelId)
    {
        await api.GetLatestVideoOrLiveAsync(channelId);
        Assert.True(true);
    }
}