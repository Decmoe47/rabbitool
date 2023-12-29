using Rabbitool.Configs;

namespace Rabbitool.Service.Test;

public class YoutubeServiceTest
{
    private readonly YoutubeService _svc;

    public YoutubeServiceTest()
    {
        Env env = Env.Load("configs.yml");

        System.Environment.SetEnvironmentVariable("http_proxy", env.Proxy!.Http);
        System.Environment.SetEnvironmentVariable("https_proxy", env.Proxy.Https);

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
