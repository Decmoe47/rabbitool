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

    private readonly string _from;
    private readonly string[] _to;
    private readonly string _host;
    private readonly int _port;
    private readonly bool _ssl;
    private readonly string _username;
    private readonly string _password;

    private readonly int _intervalMinutes;
    private readonly int _allowedAmount;
    private int _errCount;
    private long _timeStampToRefresh;

    private readonly IFormatProvider? _formatProvider;

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

    public void Emit(LogEvent logEvent)
    {
        if (logEvent.Level != LogEventLevel.Error && logEvent.Level != LogEventLevel.Fatal)
            return;

        string message = logEvent.RenderMessage(_formatProvider);
        if (logEvent.Exception != null)
            message += "\n\n" + logEvent.Exception.ToString();

        Send(message);
    }

    public async Task SendAsync(System.Exception ex, CancellationToken ct = default)
    {
        await SendAsync(ex.ToString(), ct);
    }

    public async Task SendAsync(string text, CancellationToken ct = default)
    {
        if (!Allow())
            return;

        if (!_client.IsConnected)
            await _client.ConnectAsync(_host, _port, _ssl, ct);
        if (!_client.IsAuthenticated)
            await _client.AuthenticateAsync(_username, _password, ct);

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
        if (!Allow())
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

    private bool Allow()
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        if (now > _timeStampToRefresh)
        {
            _timeStampToRefresh = now + _intervalMinutes * 60;
            _errCount = 1;
            return false;
        }

        _errCount++;
        return _errCount == _allowedAmount;
    }

    public void Dispose()
    {
        _client?.Dispose();
        GC.SuppressFinalize(this);
    }
}

public class ErrorNotifierOptions
{
    public required string Host { get; set; }
    public required int Port { get; set; }
    public required bool Ssl { get; set; }
    public required string Username { get; set; }
    public required string Password { get; set; }

    public required string From { get; set; }
    public required string[] To { get; set; }

    public required int Interval { get; set; }
    public required int AllowedAmount { get; set; }
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
