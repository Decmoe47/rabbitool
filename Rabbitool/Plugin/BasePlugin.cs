using Rabbitool.Service;

namespace Rabbitool.Plugin;

public abstract class BasePlugin
{
    protected readonly QQBotService _qbSvc;
    protected readonly CosService _cosSvc;
    protected readonly string _redirectUrl;
    protected readonly string _dbPath;

    protected BasePlugin(QQBotService qbSvc, CosService cosSvc, string dbPath, string redirectUrl)
    {
        _qbSvc = qbSvc;
        _cosSvc = cosSvc;
        _dbPath = dbPath;
        _redirectUrl = redirectUrl;
    }
}
