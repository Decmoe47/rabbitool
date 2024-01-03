using Autofac.Annotation;
using Autofac.Annotation.Condition;
using Rabbitool.Common.Constant;
using Rabbitool.Common.Provider;

namespace Rabbitool.Common.Configs;

[ConditionalOnProperty("notifier")]
[PropertySource(Constants.ConfigFilename)]
[Component]
public class NotifierConfig
{
    [Value("${notifier.host")] public required string Host { get; set; }

    [Value("${notifier.port}")] public int Port { get; set; }

    [Value("$notifier.ssl")] public bool Ssl { get; set; }

    [Value("${notifier.username}")] public required string UserName { get; set; }

    [Value("${notifier.password}")] public required string Password { get; set; }

    [Value("${notifier.from}")] public required string From { get; set; }

    [Value("${notifier.to}")] public required string[] To { get; set; }

    [Value("${notifier.interval}")] public int Interval { get; set; }

    [Value("${notifier.allowedAmount}")] public int AllowedAmount { get; set; }

    [Value("${notifier.timeout}")] public int Timeout { get; set; }

    public NotifierOptions ToOptions()
    {
        return new NotifierOptions
        {
            Host = Host,
            Port = Port,
            Ssl = Ssl,
            Username = UserName,
            Password = Password,
            From = From,
            To = To,
            Interval = Interval,
            AllowedAmount = AllowedAmount,
            Timeout = Timeout
        };
    }
}