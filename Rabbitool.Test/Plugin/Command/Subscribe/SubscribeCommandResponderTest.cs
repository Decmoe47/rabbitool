using QQChannelFramework.Models;
using QQChannelFramework.Models.MessageModels;
using Rabbitool.Conf;
using Rabbitool.Service;
using Serilog;
using Xunit.Abstractions;

namespace Rabbitool.Plugin.Command.Subscribe.Test;

public class SubscribeCommandResponderTest
{
    private readonly QQBotService _qSvc;

    public SubscribeCommandResponderTest(ITestOutputHelper output)
    {
        Environment.SetEnvironmentVariable("http_proxy", "http://127.0.0.1:7890");
        Environment.SetEnvironmentVariable("https_proxy", "http://127.0.0.1:7890");

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.TestOutput(output)
            .CreateLogger();

        Configs configs = Configs.Load("configs.yml");
        _qSvc = new QQBotService(new CosService());
        SubscribeCommandResponder.Init(_qSvc);
    }

    [Theory]
    [InlineData("b站", "2920960")]
    [InlineData("b站", "4415701")]
    [InlineData("b站", "488976342")]
    [InlineData("推特", "AliceMononobe")]
    [InlineData("推特", "kedamaa")]
    [InlineData("推特", "Genshin_7")]
    [InlineData("油管", "UCt0clH12Xk1-Ej5PXKGfdPA")]
    [InlineData("油管", "UCTkyJbRhal4voLZxmdRSssQ")]
    [InlineData("油管", "UCkIimWZ9gBJRamKF0rmPU8w")]
    public async Task RespondToSubscribeCommandAsyncTestAsync(string platform, string id)
    {
        Guild guild = (await _qSvc.GetAllGuildsAsync())[0];
        Channel channel = await _qSvc.GetChannelByNameAsync("默认", guild.Id);
        string result1 = await SubscribeCommandResponder.RespondToAddOrUpdateSubscribeCommandAsync(
            new List<string> { "/订阅", platform, id }, new Message { Id = channel.Id, GuildId = guild.Id });
        Assert.Contains("成功", result1);

        string result2 = await SubscribeCommandResponder.RespondToListSubscribeCommandAsync(
            new List<string> { "/列出订阅", platform, id }, new Message { Id = channel.Id, GuildId = guild.Id });
        Assert.Contains("=", result2);

        string result3 = await SubscribeCommandResponder.RespondToDeleteSubscribeCommandAsync(
            new List<string> { "/取消订阅", platform, id }, new Message { Id = channel.Id, GuildId = guild.Id });
        Assert.Contains("成功", result3);
    }
}