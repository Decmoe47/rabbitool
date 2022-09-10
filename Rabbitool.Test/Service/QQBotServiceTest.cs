using Newtonsoft.Json;
using QQChannelFramework.Models;
using Rabbitool.Config;
using Rabbitool.Model.DTO.QQBot;

namespace Rabbitool.Service.Test;

public class QQBotServiceTest
{
    private readonly QQBotService _svc;
    private readonly CosService _cosSvc;

    public QQBotServiceTest()
    {
        Configs configs = Configs.Load("configs.yml");

        System.Environment.SetEnvironmentVariable("http_proxy", configs.Proxy!.HttpProxy);
        System.Environment.SetEnvironmentVariable("https_proxy", configs.Proxy.HttpsProxy);

        _svc = new QQBotService(configs.QQBot.AppId, configs.QQBot.Token, true, configs.QQBot.SandboxGuildName);
        _cosSvc = new CosService(
            configs.Cos.BucketName, configs.Cos.Region, configs.Cos.SecretId, configs.Cos.SecretKey);

#pragma warning disable CS4014 // 由于此调用不会等待，因此在调用完成前将继续执行当前方法
        _svc.RunAsync();
#pragma warning restore CS4014 // 由于此调用不会等待，因此在调用完成前将继续执行当前方法
    }

    [Fact()]
    public async Task PushCommonMsgAsyncTestAsync()
    {
        Guild guild = (await _svc.GetAllGuildsAsync())[0];
        Channel channel = await _svc.GetChannelByNameAsync("默认", guild.Id);
        await _svc.PushCommonMsgAsync(channel.Id, "test123\ntest456");
        Assert.True(true);
    }

    [Fact()]
    public async Task PostThreadAsyncTestAsync()
    {
        Guild guild = (await _svc.GetAllGuildsAsync())[0];
        Channel channel = await _svc.GetChannelByNameAsync("帖子", guild.Id);

        string title = "Test";
        List<Paragraph> paragraphs = new();
        paragraphs.AddRange(QQBotService.TextToParagraphs("test123\ntest456\ntest789"));
        paragraphs.AddRange(await QQBotService.ImgagesToParagraphsAsync(
            new List<string> { "https://pbs.twimg.com/media/FbCXRYZaUAEgK0u.jpg" }, _cosSvc));
        paragraphs.AddRange(await QQBotService.VideoToParagraphsAsync(
            "https://twitter.com/sana_natori/status/1562433215671173125",
            new DateTime(2022, 8, 21, 19, 34, 00),
            _cosSvc));

        string text = JsonConvert.SerializeObject(new RichText() { Paragraphs = paragraphs });

        await _svc.PostThreadAsync(channel.Id, title, text);
        Assert.True(true);
    }
}
