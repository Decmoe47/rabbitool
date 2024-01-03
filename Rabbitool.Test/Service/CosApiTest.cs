using Rabbitool.Api;
using Xunit.Abstractions;

namespace Rabbitool.Test.Service;

public class CosApiTest(ITestOutputHelper output, CosApi api) : BaseTest
{
    [Theory]
    [InlineData("https://i0.hdslb.com/bfs/new_dyn/59543a1a4a1f06c184418aac3fe08c141609200.jpg")]
    public async Task UploadImageAsyncTestAsync(string url)
    {
        string redirectUrl = await api.UploadImageAsync(url);
        output.WriteLine($"Please click the url to check whether the image can be viewed: {redirectUrl}");
        Assert.True(true);
    }

    [Theory]
    [InlineData("https://twitter.com/sana_natori/status/1562433215671173125")]
    public async Task UploadVideoAsyncTestAsync(string url)
    {
        string redirectUrl =
            await api.UploadVideoAsync(url, new DateTime(2022, 8, 21, 19, 34, 00, DateTimeKind.Local));
        output.WriteLine($"Please click the url to check whether the video can be played: {redirectUrl}");
        Assert.True(true);
    }
}