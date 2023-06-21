using Newtonsoft.Json;
using QQChannelFramework.Models;
using QQChannelFramework.Models.Forum;
using Rabbitool.Conf;
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

        System.Environment.SetEnvironmentVariable("http_proxy", configs.Proxy!.Http);
        System.Environment.SetEnvironmentVariable("https_proxy", configs.Proxy.Https);

        _sandboxGuildName = configs.QQBot.SandboxGuildName;
        _svc = new QQBotService();
        _cosSvc = new CosService();
    }

    [Fact()]
    public async Task PushCommonMsgAsyncTestAsync()
    {
        if (!_svc.IsOnline)
            await _svc.RunAsync();

        Guild guild = (await _svc.GetAllGuildsAsync()).Single(g => g.Name == _sandboxGuildName);
        Channel channel = await _svc.GetChannelByNameAsync("默认", guild.Id);

        await _svc.PushCommonMsgAsync(channel.Id, channel.Name, "test123\ntest456");
        Assert.True(true);
    }

    [Fact()]
    public async Task PostThreadAsyncTestAsync()
    {
        if (!_svc.IsOnline)
            await _svc.RunAsync();

        Guild guild = (await _svc.GetAllGuildsAsync()).Single(g => g.Name == _sandboxGuildName);
        Channel channel = await _svc.GetChannelByNameAsync("帖子", guild.Id);

        string title = "Test";
        RichText richText = QQBotService.TextToRichText("12/23\nイブイブだしうさぎさん監視しなきゃ…♡\nの顔🎄 \r\n——————————\r\n推文发布时间：2022-12-23 06:09:18 +08:00\r\n推文链接：https://redirect-2g1tb8d680f7fddc-1302910426.ap-shanghai.app.tcloudbase.com/to/?url=https://twitter.com/AliceMononobe/status/1606049263938547712\n图片：\n");
        //RichText richText = QQBotService.TextToRichText("test1\ntest2\n\ntest3");
        richText.Paragraphs.AddRange(await QQBotService.ImagesToParagraphsAsync(
            new List<string> { "https://pbs.twimg.com/media/FbCXRYZaUAEgK0u.jpg" }, _cosSvc));
        richText.Paragraphs.AddRange(await QQBotService.VideoToParagraphsAsync(
            "https://twitter.com/sana_natori/status/1562433215671173125",
            new DateTime(2022, 8, 21, 19, 34, 00),
            _cosSvc));

        string text = JsonConvert.SerializeObject(richText);

        await _svc.PostThreadAsync(channel.Id, channel.Name, title, text);
        Assert.True(true);
    }
}
