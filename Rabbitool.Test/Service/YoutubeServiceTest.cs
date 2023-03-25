using Rabbitool.Config;

namespace Rabbitool.Service.Test;

public class YoutubeServiceTest
{
    private readonly YoutubeService _svc;

    public YoutubeServiceTest()
    {
        Configs configs = Configs.Load("configs.yml");

        System.Environment.SetEnvironmentVariable("http_proxy", configs.Proxy!.HttpProxy);
        System.Environment.SetEnvironmentVariable("https_proxy", configs.Proxy.HttpsProxy);

        _svc = new YoutubeService(configs.Youtube!.ApiKey);
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
