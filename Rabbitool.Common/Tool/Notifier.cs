using MailKit.Net.Smtp;
using MimeKit;
using Rabbitool.Common.Util;
using Serilog;
using Serilog.Configuration;
using Serilog.Core;
using Serilog.Events;

namespace Rabbitool.Common.Tool;

public class Notifier : ILogEventSink, IDisposable
{
    private readonly SmtpClient _client;
    private readonly List<ErrorCounter> _errors = new();

    private readonly IFormatProvider? _formatProvider;

    private readonly string _from;
    private readonly string _host;
    private readonly string _password;
    private readonly int _port;
    private readonly bool _ssl;
    private readonly string[] _to;
    private readonly string _username;
    
    private readonly int _allowedAmount;
    private readonly int _interval;
    private readonly int _timeout;

    public Notifier(ErrorNotifierOptions opts, IFormatProvider? formatProvider = null)
    {
        _client = new SmtpClient();

        _from = opts.From;
        _to = opts.To;
        _host = opts.Host;
        _port = opts.Port;
        _ssl = opts.Ssl;
        _username = opts.Username;
        _password = opts.Password;

        _interval = opts.Interval;
        _allowedAmount = opts.AllowedAmount;
        _timeout = opts.Timeout;

        _formatProvider = formatProvider;

        Console.CancelKeyPress += (sender, e) => Dispose();
    }

    public void Dispose()
    {
        _client.Dispose();
        GC.SuppressFinalize(this);
    }

    public void Emit(LogEvent logEvent)
    {
        if (logEvent.Level != LogEventLevel.Error && logEvent.Level != LogEventLevel.Fatal)
            return;

        string message = logEvent.RenderMessage(_formatProvider);
        string textToCount = message;
        if (logEvent.Exception != null)
        {
            message += "\n\n" + logEvent.Exception;
            if (logEvent.Exception.InnerException != null)
                textToCount += "\n\n" + logEvent.Exception.InnerException?.GetType().Name;
            else
                textToCount += "\n\n" + logEvent.Exception.GetType().Name;
        }

        Notify(message, textToCount);
    }

    public void Notify(string textToSend, string textToCount)
    {
        if (!Allow(textToCount))
            return;
        Send(textToSend);
    }
    
    private bool Allow(string text)
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        ErrorCounter? err = _errors.FirstOrDefault(e => e.Text == text);
        if (err == null)
        {
            err = new ErrorCounter(text, now + _interval * 60, now + _timeout * 60);
            _errors.Add(err);
        }
        
        err.Count++;
        // 已告警，等待超时清除
        if (err.Alerted)
        {
            if (now > err.Timeout)
                _errors.Remove(err);
            return false;
        }
        // 未告警，但已过统计时长范围
        if (now > err.TimeStampToReset)
        {
            _errors.Remove(err);
            return false;
        }
        // 未达到告警的次数
        if (err.Count <= _allowedAmount)
            return false;

        // 达到告警次数
        err.Alerted = true;
        return true;
    }
    
    private void Send(string text, bool isRecovered = false)
    {
        if (!_client.IsConnected)
            _client.Connect(_host, _port, _ssl);
        if (!_client.IsAuthenticated)
            _client.Authenticate(_username, _password);

        MimeMessage msg = new();
        msg.From.Add(new MailboxAddress(_from, _from));
        msg.To.AddRange(_to.Select(t => new MailboxAddress(t, t)));
        msg.Subject = $"Alert from rabbitool on {TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeUtil.CST):yyyy-MM-ddTHH:mm:sszzz}";

        msg.Body = new TextPart { Text = text };
        _client.Send(msg);
    }
}

public class ErrorCounter
{
    public ErrorCounter(string text, long timeStampToReset, long timeout)
    {
        Text = text;
        TimeStampToReset = timeStampToReset;
        Timeout = timeout;
    }

    public int Count { get; set; }
    public string Text { get; set; }
    public long TimeStampToReset { get; set; }
    public long Timeout { get; set; }
    public bool Alerted { get; set; }
}

public class ErrorNotifierOptions
{
    public required string Host { get; set; }
    public int Port { get; set; }
    public bool Ssl { get; set; }
    public required string Username { get; set; }
    public required string Password { get; set; }

    public required string From { get; set; }
    public required string[] To { get; set; }

    public int Interval { get; set; }
    public int AllowedAmount { get; set; }
    public int Timeout { get; set; }
}

public static class ErrorNotifierExtension
{
    public static LoggerConfiguration Mail(
        this LoggerSinkConfiguration loggerConfiguration,
        ErrorNotifierOptions errorNotifierOptions,
        IFormatProvider? formatProvider = null)
    {
        return loggerConfiguration.Sink(new Notifier(errorNotifierOptions, formatProvider));
    }
}