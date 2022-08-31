using Coravel;
using Microsoft.Extensions.Hosting;
using Rabbitool.Common.Tool;
using Rabbitool.Plugin.Command.Subscribe;
using Rabbitool.Service;
using Serilog;

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

    public AllPlugins(Configs configs)
    {
        _qbSvc = new QQBotService(configs.QQBot.AppId, configs.QQBot.Token, configs.QQBot.IsSandBox);
        _cosSvc = new CosService(configs.Cos.BucketName, configs.Cos.Region, configs.Cos.SecretId, configs.Cos.SecretKey);
        _redirectUrl = configs.RedirectUrl;
        _userAgent = configs.RedirectUrl;
        _dbPath = configs.DbPath;

        _configs = configs;

        SubscribeCommandResponder.Init(_qbSvc, _dbPath, _userAgent);

        _tokenSource = new CancellationTokenSource();
        _cancellationToken = _tokenSource.Token;
        _host = Host.CreateDefaultBuilder().ConfigureServices(services => services.AddScheduler()).Build();
    }

    public async Task RunAsync()
    {
        LogConfig.Register();
        AppDomain.CurrentDomain.ProcessExit += (sender, e) =>
        {
            _tokenSource.Cancel();
            Log.Information("Shutdown successfully.");
        };

        QQBotPlugin qPlugin = new(_qbSvc);
#pragma warning disable CS4014 // 由于此调用不会等待，因此在调用完成前将继续执行当前方法
        qPlugin.RunAsync()
            .ContinueWith(
                (task) => Console.WriteLine(task?.Exception?.InnerException?.ToString()),
                TaskContinuationOptions.OnlyOnFaulted
            );
#pragma warning restore CS4014 // 由于此调用不会等待，因此在调用完成前将继续执行当前方法

        await _host.RunAsync(_tokenSource.Token);
    }

    public void InitBilibiliPlugin()
    {
        BilibiliPlugin plugin = new(_qbSvc, _cosSvc, _dbPath, _redirectUrl, _userAgent);
        _host.Services.UseScheduler(scheduler =>
            scheduler
                .ScheduleAsync(async () => await plugin.CheckAllAsync(_cancellationToken))
                .EverySeconds(_configs.Interval.BilibiliPlugin));
    }

    public void InitTwitterPlugin()
    {
        TwitterPlugin plugin;
        if (_configs.Twitter?.ApiV2Token is string apiV2Token)
            plugin = new(apiV2Token, _qbSvc, _cosSvc, _dbPath, _redirectUrl, _userAgent);
        else if (_configs.Twitter?.XCsrfToken != null && _configs.Twitter?.Cookie != null)
            plugin = new(_configs.Twitter.XCsrfToken, _configs.Twitter.Cookie, _qbSvc, _cosSvc, _dbPath, _redirectUrl, _userAgent);
        else
            plugin = new(_qbSvc, _cosSvc, _dbPath, _redirectUrl, _userAgent);

        _host.Services.UseScheduler(scheduler =>
            scheduler
                .ScheduleAsync(async () => await plugin.CheckAllAsync(_cancellationToken))
                .EverySeconds(_configs.Interval.TwitterPlugin));
    }

    public void InitYoutubePlugin()
    {
        YoutubePlugin plugin = new(_qbSvc, _cosSvc, _dbPath, _redirectUrl, _userAgent);
        _host.Services.UseScheduler(scheduler =>
            scheduler
                .ScheduleAsync(async () => await plugin.CheckAllAsync(_cancellationToken))
                .EverySeconds(_configs.Interval.YoutubePlugin));
    }

    public void InitMailPlugin()
    {
        MailPlugin plugin = new(_qbSvc, _cosSvc, _dbPath, _redirectUrl, _userAgent);
        _host.Services.UseScheduler(scheduler =>
            scheduler
                .ScheduleAsync(async () => await plugin.CheckAllAsync(_cancellationToken))
                .EverySeconds(_configs.Interval.MailPlugin));
    }
}
