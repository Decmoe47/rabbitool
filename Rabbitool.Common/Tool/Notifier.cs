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
    private readonly int _allowedAmount;
    private readonly SmtpClient _client;
    private readonly List<ErrorCounter> _errors = new();

    private readonly IFormatProvider? _formatProvider;

    private readonly string _from;
    private readonly string _host;

    private readonly int _intervalMinutes;
    private readonly string _password;
    private readonly int _port;
    private readonly bool _ssl;
    private readonly string[] _to;
    private readonly string _username;

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

        _intervalMinutes = opts.Interval;
        _allowedAmount = opts.AllowedAmount;

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

    private void Send(string text, bool isRecovered = false)
    {
        if (!_client.IsConnected)
            _client.Connect(_host, _port, _ssl);
        if (!_client.IsAuthenticated)
            _client.Authenticate(_username, _password);

        MimeMessage msg = new();
        msg.From.Add(new MailboxAddress(_from, _from));
        foreach (string to in _to)
            msg.To.Add(new MailboxAddress(to, to));
        msg.Subject = isRecovered
            ? $"Alert Cleared from rabbitool on {TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeUtil.CST):yyyy-MM-ddTHH:mm:sszzz}"
            : $"Alert from rabbitool on {TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeUtil.CST):yyyy-MM-ddTHH:mm:sszzz}";

        msg.Body = new TextPart { Text = text };

        _client.Send(msg);
    }

    private bool Allow(string text)
    {
        ClearDisposedErrorCounter();

        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        ErrorCounter? error = _errors.FirstOrDefault(e => e.Text == text && !e.Disposed);
        if (error == null)
        {
            error = new ErrorCounter(text, now + _intervalMinutes * 60);
            _errors.Add(error);
        }

        error.Count++;
        if (now > error.TimeStampToRefresh)
        {
            error.TimeStampToRefresh = now + _intervalMinutes * 60;
            error.Count = 1;
        }

        if (error.Count <= _allowedAmount)
            return false;

        error.Alerting = true;
        return true;
    }

    private void ClearDisposedErrorCounter()
    {
        List<int> indexesToRemove = new();
        for (int i = 0; i < _errors.Count; i++)
        {
            if (!_errors[i].Disposed)
                continue;
            Send("【The error below is no longer happened】\n\n" + _errors[i].Text, true);
            indexesToRemove.Add(i);
        }

        foreach (int index in indexesToRemove)
            _errors.RemoveAt(index);
    }
}

public class ErrorCounter
{
    public ErrorCounter(string text, long timeStampToRefresh)
    {
        Text = text;
        TimeStampToRefresh = timeStampToRefresh;
        Timer = new Timer(state =>
        {
            if (!Alerting)
                return;

            Last5Count.Add(Count);
            if (Last5Count.Count > 5)
                Last5Count.RemoveAt(0);

            if (Last5Count.Count == 5 && Last5Count.All(c => c == Last5Count[0]))
            {
                Timer? t = (Timer?)state;
                t?.Dispose();
                Disposed = true;
            }
        }, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
    }

    public int Count { get; set; }
    private List<int> Last5Count { get; } = new();
    public string Text { get; set; }
    public long TimeStampToRefresh { get; set; }
    public Timer Timer { get; set; }
    public bool Alerting { get; set; }
    public bool Disposed { get; private set; }
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