using Rabbitool.Api;
using Rabbitool.Common.Configs;

namespace Rabbitool.Test.Service;

public class TwitterApiTest(TwitterApi api) : BaseTest
{
    [Theory]
    [InlineData("AliceMononobe")]
    [InlineData("amsrntk3")]
    [InlineData("kedamaa")]
    [InlineData("Genshin_7")]
    public async Task GetLatestTweetAsyncTestAsync(string screenName)
    {
        await api.GetLatestTweetAsync(screenName);
        Assert.True(true);
    }
}