using Rabbitool.Config;

namespace Rabbitool.Service.Test;

public class TwitterServiceTest
{
    private readonly TwitterService _svc;

    public TwitterServiceTest()
    {
        Configs configs = Configs.Load("configs.yml");

        System.Environment.SetEnvironmentVariable("http_proxy", configs.Proxy!.Http);
        System.Environment.SetEnvironmentVariable("https_proxy", configs.Proxy.Https);

        _svc = new TwitterService(configs.Twitter!.Token);
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
