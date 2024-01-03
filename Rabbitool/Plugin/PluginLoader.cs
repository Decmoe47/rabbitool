using Coravel;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Rabbitool.Plugin;

public static class PluginLoader
{
    public static async Task RunAllPluginsAsync(IServiceProvider serviceProvider)
    {
        foreach (IPlugin plugin in serviceProvider.GetServices<IPlugin>())
        {
            await plugin.InitAsync();
            switch (plugin)
            {
                case IScheduledPlugin scheduledPlugin:
                    serviceProvider.UseScheduler(scheduledPlugin.GetScheduler())
                        .OnError(ex =>
                            Log.Error(ex, "[" + plugin.Name + "] {msg}", ex.Message));
                    break;
                case IRunnablePlugin runnablePlugin:
                    await runnablePlugin.RunAsync();
                    break;
            }
        }
    }
}