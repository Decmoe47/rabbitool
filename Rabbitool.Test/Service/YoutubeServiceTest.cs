using Rabbitool.Common.Configs;

namespace Rabbitool.Service.Test;

public class YoutubeServiceTest
{
    private readonly YoutubeService _svc;

    public YoutubeServiceTest()
    {
        Settings settings = Settings.Load("configs.yml");

        Environment.SetEnvironmentVariable("http_proxy", settings.Proxy!.Http);
        Environment.SetEnvironmentVariable("https_proxy", settings.Proxy.Https);

        _svc = new YoutubeService();
    }

    [Theory]
    [InlineData("UCt0clH12Xk1-Ej5PXKGfdPA")]
    [InlineData("UCTkyJbRhal4voLZxmdRSssQ")]
    [InlineData("UCkIimWZ9gBJRamKF0rmPU8w")]
    public async Task GetLatestVideoOrLiveAsyncTestAsync(string channelId)
    {
        await _svc.GetLatestVideoOrLiveAsync(channelId);
        Assert.True(true);
    }
}