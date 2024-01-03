using MyBot.Models;
using MyBot.Models.Forum;
using Newtonsoft.Json;
using Rabbitool.Api;
using Rabbitool.Common.Configs;

namespace Rabbitool.Test.Service;

public class QQBotApiTest(CosApi cosApi, QQBotApi qqBotApi, QQBotConfig qqBotConfig) : BaseTest
{
    [Fact]
    public async Task PushCommonMsgAsyncTestAsync()
    {
        if (!qqBotApi.IsOnline)
            await qqBotApi.RunBotAsync();

        Guild guild = (await qqBotApi.GetAllGuildsAsync()).Single(g => g.Name == qqBotConfig.SandboxGuildName);
        Channel channel = await qqBotApi.GetChannelByNameAsync("默认", guild.Id);

        await qqBotApi.PushCommonMsgAsync(channel.Id, channel.Name, "test123\ntest456");
        Assert.True(true);
    }

    [Fact]
    public async Task PostThreadAsyncTestAsync()
    {
        if (!qqBotApi.IsOnline)
            await qqBotApi.RunBotAsync();

        Guild guild = (await qqBotApi.GetAllGuildsAsync()).Single(g => g.Name == qqBotConfig.SandboxGuildName);
        Channel channel = await qqBotApi.GetChannelByNameAsync("帖子", guild.Id);

        string title = "Test";
        RichText richText = QQBotApi.TextToRichText(
            "12/23\nイブイブだしうさぎさん監視しなきゃ…♡\nの顔🎄 \r\n——————————\r\n推文发布时间：2022-12-23 06:09:18 +08:00\r\n推文链接：https://redirect-2g1tb8d680f7fddc-1302910426.ap-shanghai.app.tcloudbase.com/to/?url=https://twitter.com/AliceMononobe/status/1606049263938547712\n图片：\n");
        //RichText richText = QQBotService.TextToRichText("test1\ntest2\n\ntest3");
        richText.Paragraphs.AddRange(await QQBotApi.ImagesToParagraphsAsync(
            ["https://pbs.twimg.com/media/FbCXRYZaUAEgK0u.jpg"], cosApi));
        richText.Paragraphs.AddRange(await QQBotApi.VideoToParagraphsAsync(
            "https://twitter.com/sana_natori/status/1562433215671173125",
            new DateTime(2022, 8, 21, 19, 34, 00),
            cosApi));

        string text = JsonConvert.SerializeObject(richText);

        await qqBotApi.PostThreadAsync(channel.Id, channel.Name, title, text);
        Assert.True(true);
    }
}