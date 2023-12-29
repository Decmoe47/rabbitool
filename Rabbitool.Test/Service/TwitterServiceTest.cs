using Rabbitool.Common.Configs;

namespace Rabbitool.Service.Test;

public class TwitterServiceTest
{
    private readonly TwitterService _svc;

    public TwitterServiceTest()
    {
        Settings settings = Settings.Load("configs.yml");

        Environment.SetEnvironmentVariable("http_proxy", settings.Proxy!.Http);
        Environment.SetEnvironmentVariable("https_proxy", settings.Proxy.Https);

        _svc = new TwitterService();
    }

    [Theory]
    [InlineData("AliceMononobe")]
    [InlineData("amsrntk3")]
    [InlineData("kedamaa")]
    [InlineData("Genshin_7")]
    public async Task GetLatestTweetAsyncTestAsync(string screenName)
    {
        await _svc.GetLatestTweetAsync(screenName);
        Assert.True(true);
    }
}