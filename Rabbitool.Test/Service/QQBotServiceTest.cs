using Newtonsoft.Json;
using QQChannelFramework.Models;
using Rabbitool.Config;
using Rabbitool.Model.DTO.QQBot;
using Xunit.Abstractions;

namespace Rabbitool.Service.Test;

public class QQBotServiceTest
{
    private readonly QQBotService _svc;
    private readonly CosService _cosSvc;
    private readonly ITestOutputHelper _output;
    private readonly string _sandboxGuildName;

    public QQBotServiceTest(ITestOutputHelper output)
    {
        _output = output;
        Configs configs = Configs.Load("configs.yml");

        System.Environment.SetEnvironmentVariable("http_proxy", configs.Proxy!.HttpProxy);
        System.Environment.SetEnvironmentVariable("https_proxy", configs.Proxy.HttpsProxy);

        _sandboxGuildName = configs.QQBot.SandboxGuildName;
        _svc = new QQBotService(configs.QQBot.AppId, configs.QQBot.Token, true, configs.QQBot.SandboxGuildName);
        _cosSvc = new CosService(
            configs.Cos.BucketName, configs.Cos.Region, configs.Cos.SecretId, configs.Cos.SecretKey);

#pragma warning disable CS4014 // 由于此调用不会等待，因此在调用完成前将继续执行当前方法
        _svc.RunAsync().ContinueWith(
            (task) =>
            {
                if (task.Exception?.InnerException is not OperationCanceledException)
                    _output.WriteLine(task.Exception?.InnerException?.ToString() ?? "");
            },
            TaskContinuationOptions.OnlyOnFaulted); ;
#pragma warning restore CS4014 // 由于此调用不会等待，因此在调用完成前将继续执行当前方法
    }

    [Fact()]
    public async Task PushCommonMsgAsyncTestAsync()
    {
        Guild guild = (await _svc.GetAllGuildsAsync()).Single(g => g.Name == _sandboxGuildName);
        Channel channel = await _svc.GetChannelByNameAsync("默认", guild.Id);

        await _svc.PushCommonMsgAsync(channel.Id, "test123\ntest456");
        Assert.True(true);
    }

    [Fact()]
    public async Task PostThreadAsyncTestAsync()
    {
        Guild guild = (await _svc.GetAllGuildsAsync()).Single(g => g.Name == _sandboxGuildName);
        Channel channel = await _svc.GetChannelByNameAsync("帖子", guild.Id);

        string title = "Test";
        RichText richText = QQBotService.TextToRichText("12/23\nイブイブだしうさぎさん監視しなきゃ…♡\nの顔🎄 \r\n——————————\r\n推文发布时间：2022-12-23 06:09:18 +08:00\r\n推文链接：https://redirect-2g1tb8d680f7fddc-1302910426.ap-shanghai.app.tcloudbase.com/to/?url=https://twitter.com/AliceMononobe/status/1606049263938547712\n图片：\n");
        //RichText richText = QQBotService.TextToRichText("test1\ntest2\n\ntest3");
        richText.Paragraphs.AddRange(await QQBotService.ImgagesToParagraphsAsync(
            new List<string> { "https://pbs.twimg.com/media/FbCXRYZaUAEgK0u.jpg" }, _cosSvc));
        richText.Paragraphs.AddRange(await QQBotService.VideoToParagraphsAsync(
            "https://twitter.com/sana_natori/status/1562433215671173125",
            new DateTime(2022, 8, 21, 19, 34, 00),
            _cosSvc));

        string text = JsonConvert.SerializeObject(richText);

        await _svc.PostThreadAsync(channel.Id, title, text);
        Assert.True(true);
    }
}
