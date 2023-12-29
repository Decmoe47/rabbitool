using Rabbitool.Configs;

namespace Rabbitool.Service.Test;

public class TwitterServiceTest
{
    private readonly TwitterService _svc;

    public TwitterServiceTest()
    {
        Env env = Env.Load("configs.yml");

        System.Environment.SetEnvironmentVariable("http_proxy", env.Proxy!.Http);
        System.Environment.SetEnvironmentVariable("https_proxy", env.Proxy.Https);

        _svc = new TwitterService();
    }

    [Theory()]
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
