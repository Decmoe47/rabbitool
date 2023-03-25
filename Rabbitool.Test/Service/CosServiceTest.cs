using Rabbitool.Config;
using Xunit.Abstractions;

namespace Rabbitool.Service.Test;

public class CosServiceTest
{
    private readonly CosService _svc;
    private readonly ITestOutputHelper _output;

    public CosServiceTest(ITestOutputHelper output)
    {
        Configs configs = Configs.Load("configs.yml");

        System.Environment.SetEnvironmentVariable("http_proxy", configs.Proxy!.Http);
        System.Environment.SetEnvironmentVariable("https_proxy", configs.Proxy.Https);

        _output = output;
        _svc = new CosService(configs.Cos.BucketName, configs.Cos.Region, configs.Cos.SecretId, configs.Cos.SecretKey);
    }

    [Theory()]
    [InlineData("https://i0.hdslb.com/bfs/new_dyn/59543a1a4a1f06c184418aac3fe08c141609200.jpg")]
    public async Task UploadImageAsyncTestAsync(string url)
    {
        string redirectUrl = await _svc.UploadImageAsync(url);
        _output.WriteLine($"Please click the url to check whether the image can be viewed: {redirectUrl}");
        Assert.True(true);
    }

    [Theory()]
    [InlineData("https://twitter.com/sana_natori/status/1562433215671173125")]
    public async Task UploadVideoAsyncTestAsync(string url)
    {
        string redirectUrl = await _svc.UploadVideoAsync(url, new DateTime(2022, 8, 21, 19, 34, 00, DateTimeKind.Local));
        _output.WriteLine($"Please click the url to check whether the video can be played: {redirectUrl}");
        Assert.True(true);
    }
}
