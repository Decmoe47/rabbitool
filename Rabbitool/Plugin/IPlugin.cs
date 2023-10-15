namespace Rabbitool.Plugin;

public interface IPlugin
{
    Task InitAsync(IServiceProvider services, CancellationToken ct = default);
}

public interface IRunnablePlugin : IPlugin
{
    Task RunAsync(CancellationToken ct = default);
}