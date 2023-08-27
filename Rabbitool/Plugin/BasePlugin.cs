using Rabbitool.Service;

namespace Rabbitool.Plugin;

public abstract class BasePlugin
{
    protected readonly CosService CosSvc;
    protected readonly QQBotService QbSvc;

    protected BasePlugin(QQBotService qbSvc, CosService cosSvc)
    {
        QbSvc = qbSvc;
        CosSvc = cosSvc;
    }
}