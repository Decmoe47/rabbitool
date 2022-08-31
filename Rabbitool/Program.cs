using Rabbitool;
using Rabbitool.Common.Tool;
using Rabbitool.Plugin;

LogConfig.Register();

Configs configs = Configs.Load("configs.yml");

AllPlugins allPlugins = new(configs);
allPlugins.InitBilibiliPlugin();
allPlugins.InitTwitterPlugin();
allPlugins.InitYoutubePlugin();
allPlugins.InitMailPlugin();

Thread.Sleep(TimeSpan.FromSeconds(3));

await allPlugins.RunAsync();
