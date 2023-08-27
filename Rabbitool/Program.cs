using Rabbitool.Common.Tool;
using Rabbitool.Conf;
using Rabbitool.Plugin;
using Rabbitool.Service;
using Serilog;

Configs conf = Configs.Load("configs.yml");

Log.Logger = conf.Notifier != null
    ? LogConfiger.New(conf.Notifier.ToOptions(), conf.DefaultLogger.ConsoleLevel, conf.DefaultLogger.FileLevel)
    : LogConfiger.New(conf.DefaultLogger.ConsoleLevel, conf.DefaultLogger.FileLevel);

Console.CancelKeyPress += (sender, e) => Log.CloseAndFlush();

if (conf is { InTestEnvironment: true, Proxy: not null })
{
    Environment.SetEnvironmentVariable("http_proxy", conf.Proxy.Http);
    Environment.SetEnvironmentVariable("https_proxy", conf.Proxy.Https);
}

CosService cosSvc = new();
QQBotService qbSvc = new(cosSvc);

PluginLoader loader = new(qbSvc);
loader.Load(new QQBotPlugin(qbSvc));
loader.Load(new BilibiliPlugin(qbSvc, cosSvc));
loader.Load(new MailPlugin(qbSvc, cosSvc));
if (conf.Twitter != null)
    loader.Load(new TwitterPlugin(qbSvc, cosSvc));
if (conf.Youtube != null)
    loader.Load(new YoutubePlugin(qbSvc, cosSvc));

CancellationTokenSource cts = new();
await loader.RunAsync(cts);