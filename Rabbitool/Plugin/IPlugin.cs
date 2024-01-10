﻿using Coravel.Invocable;

namespace Rabbitool.Plugin;

public interface IPlugin
{
    string Name { get; }

    Task InitAsync();
}

public interface IScheduledPlugin : IPlugin, IInvocable
{
}

public interface IRunnablePlugin : IPlugin
{
    Task RunAsync();
}