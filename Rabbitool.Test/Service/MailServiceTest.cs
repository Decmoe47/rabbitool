using Rabbitool.Configs;
using Rabbitool.Model.DTO.Mail;
using Xunit.Abstractions;

namespace Rabbitool.Service.Test;

public class MailServiceTest : IDisposable
{
    private readonly MailService _svc;
    private readonly ITestOutputHelper _output;

    public MailServiceTest(ITestOutputHelper output)
    {
        Env env = Env.Load("configs.yml");

        _svc = new MailService(
            "imap.126.com",
            143,
            false,
            env.Notifier!.UserName,
            env.Notifier!.Password);
        _output = output;
    }

    [Fact()]
    public async Task GetLatestMailAsyncTestAsync()
    {
        Model.DTO.Mail.Mail mail = await _svc.GetLatestMailAsync();
        _output.WriteLine(mail.Text);
        Assert.True(mail.Text != "");
    }

    public void Dispose()
    {
        _svc.Dispose();
        GC.SuppressFinalize(this);
    }
}
