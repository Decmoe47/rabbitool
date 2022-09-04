using Rabbitool.Config;

namespace Rabbitool.Service.Test;

public class TwitterServiceTest
{
    private readonly TwitterService _svc;

    public TwitterServiceTest()
    {
        Configs configs = Configs.Load("configs.yml");

        System.Environment.SetEnvironmentVariable("http_proxy", configs.Proxy!.HttpProxy);
        System.Environment.SetEnvironmentVariable("https_proxy", configs.Proxy.HttpsProxy);

        _svc = new TwitterService(configs.UserAgent, configs.Twitter!.ApiV2Token!);
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

public class TwitterServiceUsingApiV1_1Test
{
    private readonly TwitterService _svc;

    public TwitterServiceUsingApiV1_1Test()
    {
        System.Environment.SetEnvironmentVariable("http_proxy", "http://127.0.0.1:7890");
        System.Environment.SetEnvironmentVariable("https_proxy", "http://127.0.0.1:7890");

        Configs configs = Configs.Load("configs.yml");
        _svc = new TwitterService(configs.UserAgent);
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
