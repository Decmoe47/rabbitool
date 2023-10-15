using Rabbitool.Plugin.Command;
using Rabbitool.Service;

namespace Rabbitool.Plugin;

public class QQBotPlugin : IRunnablePlugin
{
    private readonly QQBotService _svc;

    public QQBotPlugin(QQBotService svc)
    {
        _svc = svc;
    }

    public async Task InitAsync(IServiceProvider services, CancellationToken ct = default)
    {
        _svc.RegisterAtMessageEvent(CommandResponder.GenerateReplyMsgAsync, ct);
    }

    public async Task RunAsync(CancellationToken ct = default)
    {
        await _svc.RunAsync();
    }
}