namespace Rabbitool.Service.Test;

public class YoutubeServiceTest
{
    private readonly YoutubeService _svc;

    public YoutubeServiceTest()
    {
        System.Environment.SetEnvironmentVariable("http_proxy", "http://127.0.0.1:7890");
        System.Environment.SetEnvironmentVariable("https_proxy", "http://127.0.0.1:7890");

        _svc = new YoutubeService();
    }

    [Theory()]
    [InlineData("UCt0clH12Xk1-Ej5PXKGfdPA")]
    [InlineData("UCTkyJbRhal4voLZxmdRSssQ")]
    [InlineData("UCkIimWZ9gBJRamKF0rmPU8w")]
    public async Task GetLatestVideoOrLiveAsyncTestAsync(string channelId)
    {
        await _svc.GetLatestVideoOrLiveAsync(channelId);
        Assert.True(true);
    }
}
