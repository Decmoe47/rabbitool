using Autofac;
using Autofac.Annotation;
using Autofac.Extensions.DependencyInjection;
using Coravel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Rabbitool.Common.Configs;
using Rabbitool.Common.Provider;
using Rabbitool.Plugin;
using Serilog;

namespace Rabbitool;

public class Program
{
    public static async Task Main(string[] args)
    {
        
        IHostBuilder builder = Host.CreateDefaultBuilder(args);
        builder.ConfigureAppConfiguration((context, configurationBuilder) =>
        {
            configurationBuilder
                .AddJsonFile("appsettings.json")
                .AddJsonFile("appsettings.Development.json", optional: true);
        });
        builder.UseServiceProviderFactory(new AutofacServiceProviderFactory());
        builder.ConfigureContainer<ContainerBuilder>((c, containerBuilder) =>
        {
            containerBuilder.RegisterModule(new AutofacAnnotationModule()
                .SetDefaultValueResource(c.Configuration)
                .SetDefaultAutofacScopeToSingleInstance());
        });
        builder.ConfigureServices(services => services.AddScheduler());
        
        using IHost host = builder.Build();
        
        // 配置logger
        LoggerConfig loggerConfig = host.Services.GetRequiredService<LoggerConfig>();
        NotifierConfig? notifierConfig = host.Services.GetService<NotifierConfig>();
        Log.Logger = notifierConfig == null
            ? LoggerConfigurationProvider.GetConfiguration(loggerConfig)
            : LoggerConfigurationProvider.GetConfiguration(loggerConfig, notifierConfig);
        Console.CancelKeyPress += (sender, e) => Log.CloseAndFlush();

        // 设置代理
        CommonConfig commonConfig = host.Services.GetRequiredService<CommonConfig>();
        if (commonConfig is { InTestEnvironment: true, Proxy: not null })
        {
            Environment.SetEnvironmentVariable("http_proxy", commonConfig.Proxy.Http);
            Environment.SetEnvironmentVariable("https_proxy", commonConfig.Proxy.Https);
        }

        await PluginLoader.RunAllPluginsAsync(host.Services);

        await host.RunAsync(host.Services.GetService<ICancellationTokenProvider>()!.Token);
    }
}