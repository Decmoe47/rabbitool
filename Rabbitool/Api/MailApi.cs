using Autofac.Annotation;
using Autofac.Annotation.Condition;
using MailKit;
using MailKit.Net.Imap;
using MimeKit;
using Rabbitool.Model.DTO.Mail;

namespace Rabbitool.Api;

[ConditionalOnProperty("mail")]
[Component]
public class MailApi(string host, int port, bool usingSsl, string username, string password, string mailbox = "INBOX")
    : IDisposable
{
    private readonly ImapClient _client = new();

    public readonly string Username = username;
    private IMailFolder? _folder;

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
            await _client.ConnectAsync(host, port, usingSsl, ct);

        if (!_client.IsAuthenticated)
        {
            await _client.AuthenticateAsync(Username, password, ct);
            ImapImplementation clientImpl = new()
            {
                Name = "rabbitool",
                Version = "1.0"
            };
            await _client.IdentifyAsync(clientImpl, ct);
        }

        if (_folder is not { IsOpen: true })
        {
            _folder = await _client.GetFolderAsync(mailbox, ct);
            await _folder.OpenAsync(FolderAccess.ReadOnly, ct);
        }

        MimeMessage msg = await _folder.GetMessageAsync(_folder.Count - 1, ct);

        List<AddressInfo> from = [];
        List<AddressInfo> to = [];
        from.AddRange(msg.From.Select(address => new AddressInfo
            { Address = address.ToString() ?? string.Empty, Name = address.Name }));
        to.AddRange(msg.To.Select(address => new AddressInfo
            { Address = address.ToString() ?? string.Empty, Name = address.Name }));

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