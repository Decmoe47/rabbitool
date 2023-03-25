using Rabbitool.Common.Tool;
using Rabbitool.Config;
using Rabbitool.Plugin;
using Rabbitool.Service;

Configs conf = Configs.Load("configs.yml");

if (conf.Notifier != null)
    LogConfiger.Register(conf.Notifier.ToOptions(), conf.Log.ConsoleLevel, conf.Log.FileLevel);
else
    LogConfiger.Register(conf.Log.ConsoleLevel, conf.Log.FileLevel);

if (conf.InTestEnvironment && conf.Proxy != null)
{
    System.Environment.SetEnvironmentVariable("http_proxy", conf.Proxy.HttpProxy);
    System.Environment.SetEnvironmentVariable("https_proxy", conf.Proxy.HttpsProxy);
}

QQBotService qbSvc = new(
            conf.QQBot.AppId, conf.QQBot.Token, conf.QQBot.IsSandBox, conf.QQBot.SandboxGuildName);
CosService cosSvc = new(
            conf.Cos.BucketName, conf.Cos.Region, conf.Cos.SecretId, conf.Cos.SecretKey);

PluginLoader loader = new(qbSvc, conf.DbPath, conf.UserAgent);
loader.Load(new QQBotPlugin(qbSvc));
if (conf.Bilibili != null)
{
    loader.Load(new BilibiliPlugin(
        qbSvc, cosSvc, conf.DbPath, conf.RedirectUrl, conf.UserAgent, conf.Bilibili.Interval));
}
if (conf.Twitter != null)
{
    loader.Load(new TwitterPlugin(
        conf.Twitter.Token, qbSvc, cosSvc, conf.DbPath, conf.RedirectUrl, conf.UserAgent, conf.Twitter.Interval));
}
if (conf.Youtube != null)
{
    loader.Load(new YoutubePlugin(
        conf.Youtube.ApiKey, qbSvc, cosSvc, conf.DbPath, conf.RedirectUrl, conf.UserAgent, conf.Youtube.Interval));
}
if (conf.Mail != null)
{
    loader.Load(new MailPlugin(
        qbSvc, cosSvc, conf.DbPath, conf.RedirectUrl, conf.UserAgent, conf.Mail.Interval));
}

CancellationTokenSource tokenSource = new();
await loader.RunAsync(tokenSource);
