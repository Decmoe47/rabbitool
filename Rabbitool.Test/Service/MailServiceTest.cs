using Rabbitool.Common.Configs;
using Xunit.Abstractions;
using Mail = Rabbitool.Model.DTO.Mail.Mail;

namespace Rabbitool.Service.Test;

public class MailServiceTest : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly MailService _svc;

    public MailServiceTest(ITestOutputHelper output)
    {
        Settings settings = Settings.Load("configs.yml");

        _svc = new MailService(
            "imap.126.com",
            143,
            false,
            settings.Notifier!.UserName,
            settings.Notifier!.Password);
        _output = output;
    }

    public void Dispose()
    {
        _svc.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task GetLatestMailAsyncTestAsync()
    {
        Mail mail = await _svc.GetLatestMailAsync();
        _output.WriteLine(mail.Text);
        Assert.True(mail.Text != "");
    }
}