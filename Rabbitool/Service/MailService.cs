using MailKit;
using MailKit.Net.Imap;
using MimeKit;
using Rabbitool.Model.DTO.Mail;

namespace Rabbitool.Service;

public class MailService : IDisposable
{
    private readonly ImapClient _client;
    private readonly string _password;
    private readonly int _port;
    private readonly bool _usingSsl;
    public readonly string Host;
    public readonly string MailBox;

    public readonly string Username;
    private IMailFolder? _folder;

    public MailService(string host, int port, bool usingSsl, string username, string password, string mailbox = "INBOX")
    {
        _client = new ImapClient();
        Host = host;
        _port = port;
        _usingSsl = usingSsl;
        Username = username;
        _password = password;
        MailBox = mailbox;
    }

    public void Dispose()
    {
        _client.Dispose();
        GC.SuppressFinalize(this);
    }

    public async Task DisconnectAsync(CancellationToken ct = default)
    {
        if (_folder != null)
            await _folder.CloseAsync(cancellationToken: ct);
        await _client.DisconnectAsync(true, ct);
    }

    public async Task<Mail> GetLatestMailAsync(CancellationToken ct = default)
    {
        if (!_client.IsConnected)
            await _client.ConnectAsync(Host, _port, _usingSsl, ct);

        if (!_client.IsAuthenticated)
        {
            await _client.AuthenticateAsync(Username, _password, ct);
            ImapImplementation clientImpl = new()
            {
                Name = "rabbitool",
                Version = "1.0"
            };
            await _client.IdentifyAsync(clientImpl, ct);
        }

        if (_folder is not { IsOpen: true })
        {
            _folder = await _client.GetFolderAsync(MailBox, ct);
            await _folder.OpenAsync(FolderAccess.ReadOnly, ct);
        }

        MimeMessage msg = await _folder.GetMessageAsync(_folder.Count - 1, ct);

        List<AddressInfo> from = new();
        List<AddressInfo> to = new();
        foreach (InternetAddress address in msg.From)
            from.Add(new AddressInfo { Address = address.ToString() ?? string.Empty, Name = address.Name });
        foreach (InternetAddress address in msg.To)
            to.Add(new AddressInfo { Address = address.ToString() ?? string.Empty, Name = address.Name });

        return new Mail
        {
            From = from,
            To = to,
            Subject = msg.Subject,
            Time = msg.Date.UtcDateTime,
            Text = msg.TextBody
        };
    }
}