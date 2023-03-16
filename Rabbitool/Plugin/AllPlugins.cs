using Coravel;
using Microsoft.Extensions.Hosting;
using Rabbitool.Common.Tool;
using Rabbitool.Config;
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
    private readonly CancellationToken _ct;
    private readonly IHost _host;

    public AllPlugins(Configs configs)
    {
        _qbSvc = new QQBotService(
            configs.QQBot.AppId, configs.QQBot.Token, configs.QQBot.IsSandBox, configs.QQBot.SandboxGuildName);
        _cosSvc = new CosService(
            configs.Cos.BucketName, configs.Cos.Region, configs.Cos.SecretId, configs.Cos.SecretKey);
        _redirectUrl = configs.RedirectUrl;
        _userAgent = configs.UserAgent;
        _dbPath = configs.DbPath;

        _configs = configs;

        SubscribeCommandResponder.Init(_qbSvc, _dbPath, _userAgent);

        _tokenSource = new CancellationTokenSource();
        _ct = _tokenSource.Token;
        _host = Host.CreateDefaultBuilder().ConfigureServices(services => services.AddScheduler()).Build();
    }

    public async Task RunAsync()
    {
        if (_configs.Notifier is not null)
            LogConfiger.Register(_configs.Notifier.ToOptions(), _configs.Log.ConsoleLevel, _configs.Log.FileLevel);
        else
            LogConfiger.Register(_configs.Log.ConsoleLevel, _configs.Log.FileLevel);

        Console.CancelKeyPress += (sender, e) => _tokenSource.Cancel();

        QQBotPlugin qPlugin = new(_qbSvc);
        await qPlugin.RunAsync();

        await _host.RunAsync(_tokenSource.Token);
    }

    public async Task InitBilibiliPluginAsync()
    {
        BilibiliPlugin plugin = new(_qbSvc, _cosSvc, _dbPath, _redirectUrl, _userAgent);
        await plugin.RefreshCookiesAsync(_ct);

        _host.Services.UseScheduler(scheduler =>
            scheduler
                .ScheduleAsync(async () => await plugin.CheckAllAsync(_ct))
                .EverySeconds(_configs.Interval.BilibiliPlugin)
                .PreventOverlapping("BilibiliPlugin"))
                .OnError(ex => Log.Error(ex, "Exception from bilibili plugin: {msg}", ex.Message));
    }

    public void InitTwitterPlugin()
    {
        TwitterPlugin plugin = new(_configs.Twitter!.Token, _qbSvc, _cosSvc, _dbPath, _redirectUrl, _userAgent);

        _host.Services.UseScheduler(scheduler =>
            scheduler
                .ScheduleAsync(async () => await plugin.CheckAllAsync(_ct))
                .EverySeconds(_configs.Interval.TwitterPlugin)
                .PreventOverlapping("TwitterPlugin"))
                .OnError(ex => Log.Error(ex, "Exception from twitter plugin: {msg}", ex.Message));
    }

    public void InitYoutubePlugin()
    {
        YoutubePlugin plugin = new(_configs.Youtube.ApiKey, _qbSvc, _cosSvc, _dbPath, _redirectUrl, _userAgent);
        _host.Services.UseScheduler(scheduler =>
            scheduler
                .ScheduleAsync(async () => await plugin.CheckAllAsync(_ct))
                .EverySeconds(_configs.Interval.YoutubePlugin)
                .PreventOverlapping("YoutubePlugin"))
                .OnError(ex => Log.Error(ex, "Exception from youtube plugin: {msg}", ex.Message));
    }

    public void InitMailPlugin()
    {
        MailPlugin plugin = new(_qbSvc, _cosSvc, _dbPath, _redirectUrl, _userAgent);
        _host.Services.UseScheduler(scheduler =>
            scheduler
                .ScheduleAsync(async () => await plugin.CheckAllAsync(_ct))
                .EverySeconds(_configs.Interval.MailPlugin)
                .PreventOverlapping("MailPlugin"))
                .OnError(ex => Log.Error(ex, "Exception from mail plugin: {msg}", ex.Message));
    }
}
