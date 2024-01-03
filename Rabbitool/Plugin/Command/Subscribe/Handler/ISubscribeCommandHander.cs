using MyBot.Models.WsModels;
using Rabbitool.Model.DTO.Command;

namespace Rabbitool.Plugin.Command.Subscribe.Handler;

internal interface ISubscribeCommandHandler
{
    Task<string> Add(SubscribeCommand command, CancellationToken ct = default);

    Task<string> Delete(SubscribeCommand command, CancellationToken ct = default);

    Task<string> List(SubscribeCommand command, CancellationToken ct = default);

    Task<(string name, string? errMsg)> CheckId(string id, CancellationToken ct = default);

    Task BotDeletedHandlerAsync(WsGuild guild, CancellationToken ct = default);
}