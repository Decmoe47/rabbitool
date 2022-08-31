using MailKit;
using MailKit.Net.Imap;
using MimeKit;
using Rabbitool.Model.DTO.Mail;

namespace Rabbitool.Service;

public class MailService : IDisposable
{
    private readonly ImapClient _client;
    private IMailFolder? _folder;
    private readonly int _port;
    private readonly string _password;
    private readonly bool _usingSsl;

    public readonly string Address;
    public readonly string Host;
    public readonly string MailBox;

    public MailService(string host, int port, bool usingSsl, string address, string password, string mailbox = "INBOX")
    {
        _client = new ImapClient();
        Host = host;
        _port = port;
        _usingSsl = usingSsl;
        Address = address;
        _password = password;
        MailBox = mailbox;
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        if (_folder is not null)
            await _folder.CloseAsync(cancellationToken: cancellationToken);
        await _client.DisconnectAsync(true, cancellationToken);
    }

    public async Task<Mail> GetLatestMailAsync(CancellationToken cancellationToken = default)
    {
        if (!_client.IsConnected)
            await _client.ConnectAsync(Host, _port, _usingSsl, cancellationToken);

        if (!_client.IsAuthenticated)
        {
            await _client.AuthenticateAsync(Address, _password, cancellationToken);
            ImapImplementation clientImpl = new()
            {
                Name = "rabbitool",
                Version = "1.0"
            };
            await _client.IdentifyAsync(clientImpl, cancellationToken);
        }

        if (_folder == null || !_folder.IsOpen)
        {
            _folder = await _client.GetFolderAsync(MailBox, cancellationToken);
            await _folder.OpenAsync(FolderAccess.ReadOnly, cancellationToken);
        }

        MimeMessage msg = await _folder.GetMessageAsync(_folder.Count - 1, cancellationToken);

        List<AddressInfo> from = new();
        List<AddressInfo> to = new();
        foreach (InternetAddress address in msg.From)
            from.Add(new AddressInfo() { Address = address.ToString(), Name = address.Name });
        foreach (InternetAddress address in msg.To)
            to.Add(new AddressInfo() { Address = address.ToString(), Name = address.Name });

        return new()
        {
            From = from,
            To = to,
            Subject = msg.Subject,
            Time = msg.Date.UtcDateTime,
            Text = msg.TextBody
        };
    }

    public void Dispose()
    {
        _client.Dispose();
        GC.SuppressFinalize(this);
    }
}
