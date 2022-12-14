using Rabbitool.Plugin.Command;
using Rabbitool.Service;

namespace Rabbitool.Plugin;

public class QQBotPlugin
{
    private readonly QQBotService _svc;

    public QQBotPlugin(QQBotService svc)
    {
        _svc = svc;
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        _svc.RegisterAtMessageEvent(CommandResponder.GenerateReplyMsgAsync, cancellationToken);
        await _svc.RunAsync();
    }
}
