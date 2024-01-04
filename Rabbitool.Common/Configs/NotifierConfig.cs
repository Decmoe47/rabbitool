using Autofac.Annotation;
using Autofac.Annotation.Condition;
using Rabbitool.Common.Constant;
using Rabbitool.Common.Provider;

namespace Rabbitool.Common.Configs;

[ConditionalOnProperty("notifier:enabled", "True")]
[PropertySource(Constants.ConfigFilename)]
[Component]
public class NotifierConfig
{
    [Value("${notifier:enabled}")] public bool Enable { get; set; }

    [Value("${notifier:host}")] public string Host { get; set; } = null!;

    [Value("${notifier:port}")] public int Port { get; set; }

    [Value("${notifier:ssl}")] public bool Ssl { get; set; }

    [Value("${notifier:username}")] public string UserName { get; set; } = null!;

    [Value("${notifier:password}")] public string Password { get; set; } = null!;

    [Value("${notifier:from}")] public string From { get; set; } = null!;

    [Value("notifier:to", UseSpel = false)]
    public string[] To { get; set; } = null!;

    [Value("${notifier:interval}")] public int Interval { get; set; }

    [Value("${notifier:allowedAmount}")] public int AllowedAmount { get; set; }

    [Value("${notifier:timeout}")] public int Timeout { get; set; }

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