using MyBot.Models;
using MyBot.Models.MessageModels;
using Rabbitool.Api;
using Rabbitool.Plugin.Command.Subscribe;

namespace Rabbitool.Test.Plugin.Command.Subscribe;

public class SubscribeCommandsTest(QQBotApi api, SubscribeCommands subscribeCommands)
{
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
        Guild guild = (await api.GetAllGuildsAsync())[0];
        Channel channel = await api.GetChannelByNameAsync("默认", guild.Id);
        string result1 = await subscribeCommands.RespondToAddOrUpdateSubscribeCommandAsync(
            ["/订阅", platform, id], new Message { Id = channel.Id, GuildId = guild.Id });
        Assert.Contains("成功", result1);

        string result2 = await subscribeCommands.RespondToListSubscribeCommandAsync(
            ["/列出订阅", platform, id], new Message { Id = channel.Id, GuildId = guild.Id });
        Assert.Contains("=", result2);

        string result3 = await subscribeCommands.RespondToDeleteSubscribeCommandAsync(
            ["/取消订阅", platform, id], new Message { Id = channel.Id, GuildId = guild.Id });
        Assert.Contains("成功", result3);
    }
}