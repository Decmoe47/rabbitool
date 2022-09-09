using MailKit.Net.Smtp;
using MimeKit;
using Rabbitool.Common.Util;
using Serilog;
using Serilog.Configuration;
using Serilog.Core;
using Serilog.Events;

namespace Rabbitool.Common.Tool;

public class ErrorNotifier : ILogEventSink, IDisposable
{
    private readonly SmtpClient _client;

    private readonly string _from;
    private readonly string[] _to;
    private readonly string _host;
    private readonly int _port;
    private readonly bool _ssl;
    private readonly string _username;
    private readonly string _password;

    private readonly int _intervalMinutes;
    private readonly int _allowedAmount;
    private readonly List<ErrorCounter> _errorCounters;

    private readonly IFormatProvider? _formatProvider;

    public ErrorNotifier(ErrorNotifierOptions opts, IFormatProvider? formatProvider = null)
    {
        _client = new SmtpClient();
        _errorCounters = new List<ErrorCounter>();

        _from = opts.From;
        _to = opts.To;

        _host = opts.Host;
        _port = opts.Port;
        _ssl = opts.Ssl;
        _username = opts.Username;
        _password = opts.Password;

        _intervalMinutes = opts.RefreshMinutes;
        _allowedAmount = opts.AllowedAmount;

        _formatProvider = formatProvider;

        AppDomain.CurrentDomain.ProcessExit += (sender, e) => Dispose();
    }

    public void Emit(LogEvent logEvent)
    {
        if (logEvent.Level != LogEventLevel.Error && logEvent.Level != LogEventLevel.Fatal)
            return;

        string message = logEvent.RenderMessage(_formatProvider);
        if (logEvent.Exception != null)
            message += "\n\n" + logEvent.Exception.ToString();

        Send(message);
    }

    public async Task SendAsync(System.Exception ex, CancellationToken cancellationToken = default)
    {
        await SendAsync(ex.ToString(), cancellationToken);
    }

    public async Task SendAsync(string text, CancellationToken cancellationToken = default)
    {
        if (!Allow(text))
            return;

        if (!_client.IsConnected)
            await _client.ConnectAsync(_host, _port, _ssl, cancellationToken);
        if (!_client.IsAuthenticated)
            await _client.AuthenticateAsync(_username, _password, cancellationToken);

        MimeMessage msg = new();
        msg.From.Add(new MailboxAddress(_from, _from));
        foreach (string to in _to)
            msg.To.Add(new MailboxAddress(to, to));
        msg.Subject = $"Error from rabbitool on {TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeUtil.CST):yyyy-MM-ddTHH:mm:sszzz}";
        msg.Body = new TextPart() { Text = text };

        await _client.SendAsync(msg);
    }

    public void Send(System.Exception ex)
    {
        Send(ex.ToString());
    }

    public void Send(string text)
    {
        if (!Allow(text))
            return;

        if (!_client.IsConnected)
            _client.Connect(_host, _port, _ssl);
        if (!_client.IsAuthenticated)
            _client.Authenticate(_username, _password);

        MimeMessage msg = new();
        msg.From.Add(new MailboxAddress(_from, _from));
        foreach (string to in _to)
            msg.To.Add(new MailboxAddress(to, to));
        msg.Subject = $"Error from rabbitool on {TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeUtil.CST):yyyy-MM-ddTHH:mm:sszzz}";
        msg.Body = new TextPart() { Text = text };

        _client.Send(msg);
    }

    private bool Allow(string text)
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        int i = _errorCounters.FindIndex(e => e.Text == text);
        if (i == -1)
        {
            i = _errorCounters.Count;
            _errorCounters.Add(new ErrorCounter(text, now + (_intervalMinutes * 60)));
        }
        else
        {
            _errorCounters[i].Amount++;
        }

        if (now < _errorCounters[i].TimestampToRefresh && _errorCounters[i].Amount >= _allowedAmount)
        {
            _errorCounters.RemoveAt(i);
            return true;
        }

        return false;
    }

    public void Dispose()
    {
        _client?.Dispose();
        GC.SuppressFinalize(this);
    }
}

internal class ErrorCounter
{
    public string Text { get; set; }
    public int Amount { get; set; } = 1;
    public long TimestampToRefresh { get; set; }

    public ErrorCounter(string text, long timestampToRefresh)
    {
        Text = text;
        TimestampToRefresh = timestampToRefresh;
    }
}

public class ErrorNotifierOptions
{
    public string Host { get; set; } = null!;
    public int Port { get; set; }
    public bool Ssl { get; set; }
    public string Username { get; set; } = null!;
    public string Password { get; set; } = null!;

    public string From { get; set; } = null!;
    public string[] To { get; set; } = null!;

    public int RefreshMinutes { get; set; }
    public int MaxAmount { get; set; }
    public int AllowedAmount { get; set; }
}

public static class ErrorNotifierExtension
{
    public static LoggerConfiguration Mail(
        this LoggerSinkConfiguration loggerConfiguration,
        ErrorNotifierOptions errorNotifierOptions,
        IFormatProvider? formatProvider = null)
    {
        return loggerConfiguration.Sink(new ErrorNotifier(errorNotifierOptions, formatProvider));
    }
}
