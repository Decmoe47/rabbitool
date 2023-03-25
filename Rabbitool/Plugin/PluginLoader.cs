using Coravel;
using Microsoft.Extensions.Hosting;
using Rabbitool.Plugin.Command.Subscribe;
using Rabbitool.Service;

namespace Rabbitool.Plugin;

public class PluginLoader
{
    private readonly IHost _host;
    private readonly List<IPlugin> _plugins = new();

    public PluginLoader(QQBotService qbSvc, string dbPath, string userAgent)
    {
        SubscribeCommandResponder.Init(qbSvc, dbPath, userAgent);
        _host = Host.CreateDefaultBuilder().ConfigureServices(services => services.AddScheduler()).Build();
    }

    public void Load(IPlugin plugin)
    {
        _plugins.Add(plugin);
    }

    public async Task RunAsync(CancellationTokenSource cts)
    {
        Console.CancelKeyPress += (sender, e) => cts.Cancel();

        foreach (IPlugin plugin in _plugins)
        {
            await plugin.InitAsync(_host.Services, cts.Token);
            if (plugin is IRunnablePlugin p)
                await p.RunAsync(cts.Token);
        }

        await _host.RunAsync(cts.Token);
    }
}
