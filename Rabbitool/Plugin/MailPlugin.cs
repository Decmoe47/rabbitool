using System.Text.RegularExpressions;
using Autofac.Annotation;
using Autofac.Annotation.Condition;
using Coravel.Invocable;
using MyBot.Models.Forum;
using Newtonsoft.Json;
using Rabbitool.Api;
using Rabbitool.Common.Configs;
using Rabbitool.Common.Provider;
using Rabbitool.Common.Util;
using Rabbitool.Event;
using Rabbitool.Model.Entity.Subscribe;
using Rabbitool.Repository.Subscribe;
using Serilog;
using Mail = Rabbitool.Model.DTO.Mail.Mail;

namespace Rabbitool.Plugin;

[ConditionalOnProperty("mail:enabled", "True")]
[Component]
public partial class MailPlugin : IScheduledPlugin, ICancellableInvocable
{
    private static readonly Dictionary<string, Dictionary<DateTime, Mail>> StoredMails = new();
    private readonly List<MailApi> _apis = [];
    private readonly CommonConfig _commonConfig;
    private readonly MailSubscribeConfigRepository _configRepo;
    private readonly ICancellationTokenProvider _ctp;
    private readonly QQBotApi _qqBotApi;
    private readonly MailSubscribeRepository _repo;

    /// <summary>
    ///     会同时注册<see cref="MailSubscribeEvent.AddMailSubscribeEvent" />
    ///     和<see cref="MailSubscribeEvent.DeleteMailSubscribeEvent" />
    ///     和<see cref="Console.CancelKeyPress" />事件。
    /// </summary>
    public MailPlugin(QQBotApi qqBotApi, MailSubscribeConfigRepository configRepo, MailSubscribeRepository repo,
        ICancellationTokenProvider ctp, CommonConfig commonConfig)
    {
        _qqBotApi = qqBotApi;
        _configRepo = configRepo;
        _repo = repo;
        _ctp = ctp;
        _commonConfig = commonConfig;

        MailSubscribeEvent.AddMailSubscribeEvent += HandleMailSubscribeAddedEvent;
        MailSubscribeEvent.DeleteMailSubscribeEvent += HandleMailSubscribeDeletedEventAsync;
        Console.CancelKeyPress += DisposeAllServices;
    }

    public CancellationToken CancellationToken { get; set; }
    public string Name => "mail";

    public Task InitAsync()
    {
        return Task.CompletedTask;
    }

    public async Task Invoke()
    {
        await CheckAllAsync();
    }

    private async Task CheckAllAsync()
    {
        if (CancellationToken.IsCancellationRequested)
            return;

        List<MailSubscribeEntity> records = await _repo.GetAllAsync(true, _ctp.Token);
        if (records.Count == 0)
        {
            Log.Verbose("[Mail] There isn't any mail subscribe yet!");
            return;
        }

        List<Task> tasks = [];
        foreach (MailSubscribeEntity record in records)
        {
            MailApi? api = _apis.FirstOrDefault(s => s.Username == record.Address);
            if (api == null)
            {
                api = new MailApi(
                    record.Host, record.Port, record.Ssl, record.Username, record.Password, record.Mailbox);
                _apis.Add(api);
            }

            tasks.Add(CheckAsync(api, record));
        }

        await Task.WhenAll(tasks);
    }

    private async Task CheckAsync(MailApi svc, MailSubscribeEntity record)
    {
        try
        {
            Mail mail = await svc.GetLatestMailAsync(_ctp.Token);
            if (mail.Time <= record.LastMailTime)
            {
                Log.Debug("[Mail] No new mail from the mail user {username}", record.Username);
                return;
            }

            async Task FnAsync(Mail mail)
            {
                await PushMessageAsync(mail, record);

                record.LastMailTime = mail.Time;
                await _repo.SaveAsync(_ctp.Token);
                Log.Debug("[Mail] Succeeded to updated the mail user {username}'s record.", record.Username);
            }

            DateTime now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeUtil.CST);
            if (now.Hour is >= 0 and <= 5)
            {
                if (!StoredMails.ContainsKey(record.Username))
                    StoredMails[record.Username] = new Dictionary<DateTime, Mail>();
                if (!StoredMails[record.Username].ContainsKey(mail.Time))
                    StoredMails[record.Username][mail.Time] = mail;

                Log.Debug("[Mail] Mail message of the user {username} is skipped because it's curfew time now.",
                    record.Username);
                return;
            }

            if (StoredMails.TryGetValue(record.Username, out Dictionary<DateTime, Mail>? storedMails)
                && storedMails.Count != 0)
            {
                List<DateTime> times = storedMails.Keys.ToList();
                times.Sort();
                foreach (DateTime time in times)
                {
                    await FnAsync(storedMails[time]);
                    StoredMails[record.Username].Remove(time);
                }

                return;
            }

            await FnAsync(mail);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[Mail] Failed to push mail message!\nAddress: {username}", record.Username);
        }
    }

    private async Task PushMessageAsync(Mail mail, MailSubscribeEntity record)
    {
        (string title, string text, string detailText) = MailToStr(mail);

        List<MailSubscribeConfigEntity> configs = await _configRepo.GetAllAsync(record.Address, ct: _ctp.Token);
        foreach (QQChannelSubscribeEntity channel in record.QQChannels)
        {
            if (!await _qqBotApi.ExistChannelAsync(channel.ChannelId))
            {
                Log.Warning("[Mail] The channel {channelName}(id: {channelId}) doesn't exist!",
                    channel.ChannelName, channel.ChannelId);
                continue;
            }

            MailSubscribeConfigEntity config = configs.First(c => c.QQChannel.ChannelId == channel.ChannelId);
            if (config.PushToThread)
            {
                RichText richText = config.Detail
                    ? QQBotApi.TextToRichText(detailText)
                    : QQBotApi.TextToRichText(text);
                await _qqBotApi.PostThreadAsync(
                    channel.ChannelId, channel.ChannelName, title, JsonConvert.SerializeObject(richText), _ctp.Token);
                Log.Information("[Mail] Succeeded to push the mail message from the user {username}).",
                    record.Username);
                continue;
            }

            if (config.Detail)
                await _qqBotApi.PushCommonMsgAsync(channel.ChannelId, channel.ChannelName, $"{title}\n\n{detailText}",
                    ct: _ctp.Token);
            else
                await _qqBotApi.PushCommonMsgAsync(channel.ChannelId, channel.ChannelName, $"{title}\n\n{text}",
                    ct: _ctp.Token);
            Log.Information("[Mail] Succeeded to push the mail message from the user {username}).", record.Username);
        }
    }

    private (string title, string text, string detailText) MailToStr(Mail mail)
    {
        string from = mail.From.Aggregate("", (current, item) => current + $"{item.Address} ");
        string to = mail.To.Aggregate("", (current, item) => current + $"{item.Address} ");
        string title = "【新邮件】来自 " + from;
        string text = mail.Text.AddRedirectToUrls(_commonConfig.RedirectUrl);

        text = MyRegex().Replace(text, match => match.ToString().Replace(".", "*"));

        string detailText = $"""
                             Subject: {mail.Subject}
                             To: {to}
                             Time: {mail.Time:yyyy-M-d H:mm}
                             ——————————
                             {mail.Text.AddRedirectToUrls(_commonConfig.RedirectUrl)}
                             """;
        detailText = MyRegex().Replace(detailText, match => match.ToString().Replace(".", "*"));

        return (title, text, detailText);
    }

    private void HandleMailSubscribeAddedEvent(
        string host, int port, bool usingSsl, string address, string password, string mailbox)
    {
        if (_apis.FindIndex(s => s.Username == address) == -1)
            _apis.Add(new MailApi(host, port, usingSsl, address, password, mailbox));
    }

    private async Task HandleMailSubscribeDeletedEventAsync(string address, CancellationToken ct)
    {
        MailApi? api = _apis.FirstOrDefault(s => s.Username == address);
        if (api != null)
            try
            {
                await api.DisconnectAsync(ct);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[Mail] Failed to disconnect the mail client!\nUsername: {username}", api.Username);
            }
            finally
            {
                api.Dispose();
                _apis.Remove(api);
            }
    }

    private void DisposeAllServices(object? sender, EventArgs e)
    {
        foreach (MailApi svc in _apis)
            svc.Dispose();
    }

    [GeneratedRegex(@"[A-Za-z0-9-_\u4e00-\u9fa5]+@[a-zA-Z0-9_-]+(\.[a-zA-Z0-9_-]+)+")]
    private static partial Regex MyRegex();
}