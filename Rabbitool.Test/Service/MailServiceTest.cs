using Rabbitool.Config;
using Rabbitool.Model.DTO.Mail;
using Xunit.Abstractions;

namespace Rabbitool.Service.Test;

public class MailServiceTest : IDisposable
{
    private readonly MailService _svc;
    private readonly ITestOutputHelper _output;

    public MailServiceTest(ITestOutputHelper output)
    {
        Configs configs = Configs.Load("configs.yml");

        _svc = new MailService(
            "imap.126.com",
            143,
            false,
            configs.ErrorNotifier!.SenderUsername,
            configs.ErrorNotifier!.SenderPassword);
        _output = output;
    }

    [Fact()]
    public async Task GetLatestMailAsyncTestAsync()
    {
        Mail mail = await _svc.GetLatestMailAsync();
        _output.WriteLine(mail.Text);
        Assert.True(mail.Text != "");
    }

    public void Dispose()
    {
        _svc.Dispose();
        GC.SuppressFinalize(this);
    }
}
