using Rabbitool;
using Rabbitool.Plugin;

Configs configs = Configs.Load("configs.yml");

if (configs.InTestEnvironment && configs.HttpProxy != null && configs.HttpsProxy != null)
{
    System.Environment.SetEnvironmentVariable("http_proxy", configs.HttpProxy);
    System.Environment.SetEnvironmentVariable("https_proxy", configs.HttpsProxy);
}

AllPlugins allPlugins = new(configs);
allPlugins.InitBilibiliPlugin();
allPlugins.InitTwitterPlugin();
allPlugins.InitYoutubePlugin();
allPlugins.InitMailPlugin();

await allPlugins.RunAsync();
