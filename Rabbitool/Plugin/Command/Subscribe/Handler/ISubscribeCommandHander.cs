using QQChannelFramework.Models.WsModels;
using Rabbitool.Model.DTO.Command;

namespace Rabbitool.Plugin.Command.Subscribe;

internal interface ISubscribeCommandHandler
{
    Task<string> Add(SubscribeCommandDTO command, CancellationToken ct = default);

    Task<string> Delete(SubscribeCommandDTO command, CancellationToken ct = default);

    Task<string> List(SubscribeCommandDTO command, CancellationToken ct = default);

    Task<(string name, string? errMsg)> CheckId(string id, CancellationToken ct = default);

    Task BotDeletedHandlerAsync(WsGuild guild, CancellationToken ct);
}
