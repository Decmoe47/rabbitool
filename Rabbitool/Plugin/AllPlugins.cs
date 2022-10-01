using Coravel;
using Microsoft.Extensions.Hosting;
using Rabbitool.Common.Tool;
using Rabbitool.Config;
using Rabbitool.Event;
using Rabbitool.Plugin.Command.Subscribe;
using Rabbitool.Service;
using Log = Serilog.Log;

namespace Rabbitool.Plugin;

public class AllPlugins
{
    private readonly QQBotService _qbSvc;
    private readonly CosService _cosSvc;
    private readonly string _redirectUrl;
    private readonly string _userAgent;
    private readonly string _dbPath;

    private readonly Configs _configs;

    private readonly CancellationTokenSource _tokenSource;
    private readonly CancellationToken _cancellationToken;
    private readonly IHost _host;

    private Dictionary<string, bool> _pluginSwitches;

    public AllPlugins(Configs configs)
    {
        _qbSvc = new QQBotService(configs.QQBot.AppId, configs.QQBot.Token, configs.QQBot.IsSandBox, configs.QQBot.SandboxGuildName);
        _cosSvc = new CosService(configs.Cos.BucketName, configs.Cos.Region, configs.Cos.SecretId, configs.Cos.SecretKey);
        _redirectUrl = configs.RedirectUrl;
        _userAgent = configs.UserAgent;
        _dbPath = configs.DbPath;

        _configs = configs;

        _pluginSwitches = new Dictionary<string, bool>();
        PluginSwitchEvent.SwitchBilibiliPluginEvent += HandleBilibiliPluginSwitchEvent;
        PluginSwitchEvent.SwitchTwitterPluginEvent += HandleTwitterPluginSwitchEvent;
        PluginSwitchEvent.SwitchYoutubePluginEvent += HandleYoutubePluginSwitchEvent;
        PluginSwitchEvent.SwitchMailPluginEvent += HandleMailPluginSwitchEvent;
        PluginSwitchEvent.GetPluginSwitchesEvent += HandleGetPluginSwitchesEvent;

        SubscribeCommandResponder.Init(_qbSvc, _dbPath, _userAgent);

        _tokenSource = new CancellationTokenSource();
        _cancellationToken = _tokenSource.Token;
        _host = Host.CreateDefaultBuilder().ConfigureServices(services => services.AddScheduler()).Build();
    }

    public async Task RunAsync()
    {
        if (_configs.ErrorNotifier is not null)
            LogConfig.Register(_configs.ErrorNotifier.ToOptions(), _configs.Log.ConsoleLevel, _configs.Log.FileLevel);
        else
            LogConfig.Register(_configs.Log.ConsoleLevel, _configs.Log.FileLevel);

        Console.CancelKeyPress += (sender, e) => _tokenSource.Cancel();

        QQBotPlugin qPlugin = new(_qbSvc);
#pragma warning disable CS4014 // 由于此调用不会等待，因此在调用完成前将继续执行当前方法
        qPlugin.RunAsync().ContinueWith(
            (task) =>
            {
                if (task.Exception?.InnerException is not OperationCanceledException)
                    Log.Error(task.Exception?.InnerException?.ToString() ?? "");
            },
            TaskContinuationOptions.OnlyOnFaulted);
#pragma warning restore CS4014 // 由于此调用不会等待，因此在调用完成前将继续执行当前方法

        await _host.RunAsync(_tokenSource.Token);
    }

    public async Task InitBilibiliPluginAsync()
    {
        _pluginSwitches["bilibili"] = true;
        BilibiliPlugin plugin = new(_qbSvc, _cosSvc, _dbPath, _redirectUrl, _userAgent);
        await plugin.RefreshCookiesAsync(_cancellationToken);

        _host.Services.UseScheduler(scheduler =>
            scheduler
                .ScheduleAsync(async () =>
                {
                    if (_pluginSwitches["bilibili"])
                        await plugin.CheckAllAsync(_cancellationToken);
                })
                .EverySeconds(_configs.Interval.BilibiliPlugin)
                .PreventOverlapping("BilibiliPlugin"));
    }

    public void InitTwitterPlugin()
    {
        _pluginSwitches["twitter"] = true;
        TwitterPlugin plugin;
        if (_configs.Twitter?.ApiV2Token is string apiV2Token)
            plugin = new(apiV2Token, _qbSvc, _cosSvc, _dbPath, _redirectUrl, _userAgent);
        else if (_configs.Twitter?.XCsrfToken != null && _configs.Twitter?.Cookie != null)
            plugin = new(_configs.Twitter.XCsrfToken, _configs.Twitter.Cookie, _qbSvc, _cosSvc, _dbPath, _redirectUrl, _userAgent);
        else
            plugin = new(_qbSvc, _cosSvc, _dbPath, _redirectUrl, _userAgent);

        _host.Services.UseScheduler(scheduler =>
            scheduler
                .ScheduleAsync(async () =>
                {
                    if (_pluginSwitches["twitter"])
                        await plugin.CheckAllAsync(_cancellationToken);
                })
                .EverySeconds(_configs.Interval.TwitterPlugin)
                .PreventOverlapping("TwitterPlugin"));
    }

    public void InitYoutubePlugin()
    {
        _pluginSwitches["youtube"] = true;
        YoutubePlugin plugin = new(_configs.Youtube.ApiKey, _qbSvc, _cosSvc, _dbPath, _redirectUrl, _userAgent);
        _host.Services.UseScheduler(scheduler =>
            scheduler
                .ScheduleAsync(async () =>
                {
                    if (_pluginSwitches["youtube"])
                        await plugin.CheckAllAsync(_cancellationToken);
                })
                .EverySeconds(_configs.Interval.YoutubePlugin)
                .PreventOverlapping("YoutubePlugin"));
    }

    public void InitMailPlugin()
    {
        _pluginSwitches["mail"] = true;
        MailPlugin plugin = new(_qbSvc, _cosSvc, _dbPath, _redirectUrl, _userAgent);
        _host.Services.UseScheduler(scheduler =>
            scheduler
                .ScheduleAsync(async () =>
                {
                    if (_pluginSwitches["mail"])
                        await plugin.CheckAllAsync(_cancellationToken);
                })
                .EverySeconds(_configs.Interval.MailPlugin)
                .PreventOverlapping("MailPlugin"));
    }

    private void HandleBilibiliPluginSwitchEvent(bool status)
    {
        _pluginSwitches["bilibili"] = status;
    }

    private void HandleYoutubePluginSwitchEvent(bool status)
    {
        _pluginSwitches["youtube"] = status;
    }

    private void HandleTwitterPluginSwitchEvent(bool status)
    {
        _pluginSwitches["twitter"] = status;
    }

    private void HandleMailPluginSwitchEvent(bool status)
    {
        _pluginSwitches["mail"] = status;
    }

    private Dictionary<string, bool> HandleGetPluginSwitchesEvent()
    {
        return _pluginSwitches;
    }
}
