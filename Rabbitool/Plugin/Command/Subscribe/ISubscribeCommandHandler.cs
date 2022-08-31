using QQChannelFramework.Models.WsModels;
using Rabbitool.Model.DTO.Command;

namespace Rabbitool.Plugin.Command.Subscribe;

internal interface ISubscribeCommandHandler
{
    Task<string> Add(SubscribeCommandDTO command, CancellationToken cancellationToken = default);

    Task<string> Delete(SubscribeCommandDTO command, CancellationToken cancellationToken = default);

    Task<string> List(SubscribeCommandDTO command, CancellationToken cancellationToken = default);

    Task<(string name, string? errCommandMsg)> CheckId(string id, CancellationToken cancellationToken = default);

    Task BotDeletedHandlerAsync(WsGuild guild, CancellationToken cancellationToken);
}
