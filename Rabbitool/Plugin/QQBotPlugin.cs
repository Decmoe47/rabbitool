using Autofac.Annotation;
using Rabbitool.Api;
using Rabbitool.Common.Provider;
using Rabbitool.Plugin.Command;

namespace Rabbitool.Plugin;

[Component(AutofacScope = AutofacScope.SingleInstance)]
public class QQBotPlugin(QQBotApi api, Commands commands, ICancellationTokenProvider ctp) : IRunnablePlugin
{
    public string Name => "qqBot";

    public Task InitAsync()
    {
        api.RegisterAtMessageEvent(commands.GenerateReplyMsgAsync, ctp.Token);
        return Task.CompletedTask;
    }

    public async Task RunAsync()
    {
        await api.RunBotAsync();
    }
}