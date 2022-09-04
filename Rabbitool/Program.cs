using Rabbitool.Config;
using Rabbitool.Plugin;

Configs configs = Configs.Load("configs.yml");

if (configs.InTestEnvironment && configs.Proxy?.HttpProxy != null && configs.Proxy?.HttpsProxy != null)
{
    System.Environment.SetEnvironmentVariable("http_proxy", configs.Proxy.HttpProxy);
    System.Environment.SetEnvironmentVariable("https_proxy", configs.Proxy.HttpsProxy);
}

AllPlugins allPlugins = new(configs);
allPlugins.InitBilibiliPlugin();
allPlugins.InitTwitterPlugin();
allPlugins.InitYoutubePlugin();
allPlugins.InitMailPlugin();

await allPlugins.RunAsync();
