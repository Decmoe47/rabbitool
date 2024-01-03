using Coravel.Scheduling.Schedule.Interfaces;

namespace Rabbitool.Plugin;

public interface IPlugin
{
    string Name { get; }

    Task InitAsync();
}

public interface IScheduledPlugin : IPlugin
{
    Action<IScheduler> GetScheduler();
}

public interface IRunnablePlugin : IPlugin
{
    Task RunAsync();
}