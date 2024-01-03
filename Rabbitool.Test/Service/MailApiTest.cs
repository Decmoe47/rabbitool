using Rabbitool.Api;
using Rabbitool.Common.Configs;
using Xunit.Abstractions;
using Mail = Rabbitool.Model.DTO.Mail.Mail;

namespace Rabbitool.Test.Service;

public class MailApiTest(ITestOutputHelper output, NotifierConfig notifierConfig) : BaseTest, IDisposable
{
    private readonly MailApi _api = new(
        "imap.126.com",
        143,
        false,
        notifierConfig.UserName,
        notifierConfig.Password);

    public void Dispose()
    {
        _api.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task GetLatestMailAsyncTestAsync()
    {
        Mail mail = await _api.GetLatestMailAsync();
        output.WriteLine(mail.Text);
        Assert.NotEqual("", mail.Text);
    }
}