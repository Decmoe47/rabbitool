using Rabbitool.Service;

namespace Rabbitool.Plugin;

public abstract class BasePlugin
{
    protected readonly QQBotService _qbSvc;
    protected readonly CosService _cosSvc;

    protected BasePlugin(QQBotService qbSvc, CosService cosSvc)
    {
        _qbSvc = qbSvc;
        _cosSvc = cosSvc;
    }
}