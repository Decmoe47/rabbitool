using System.Text.RegularExpressions;
using Coravel;
using Newtonsoft.Json;
using QQChannelFramework.Models.Forum;
using Rabbitool.Common.Util;
using Rabbitool.Conf;
using Rabbitool.Event;
using Rabbitool.Model.DTO.Mail;
using Rabbitool.Model.Entity.Subscribe;
using Rabbitool.Repository.Subscribe;
using Rabbitool.Service;
using Serilog;
using Mail = Rabbitool.Model.DTO.Mail.Mail;

namespace Rabbitool.Plugin;

public class MailPlugin : BasePlugin, IPlugin
{
    private readonly MailSubscribeConfigRepository _configRepo;
    private readonly MailSubscribeRepository _repo;
    private readonly List<MailService> _services = new();

    private readonly Dictionary<string, Dictionary<DateTime, Mail>> _storedMails = new();

    /// <summary>
    ///     会同时注册<see cref="MailSubscribeEvent.AddMailSubscribeEvent" />
    ///     和<see cref="MailSubscribeEvent.DeleteMailSubscribeEvent" />
    ///     和<see cref="Console.CancelKeyPress" />事件。
    /// </summary>
    public MailPlugin(QQBotService qbSvc, CosService cosSvc) : base(qbSvc, cosSvc)
    {
        SubscribeDbContext dbCtx = new(Configs.R.DbPath);
        _repo = new MailSubscribeRepository(dbCtx);
        _configRepo = new MailSubscribeConfigRepository(dbCtx);

        MailSubscribeEvent.AddMailSubscribeEvent += HandleMailSubscribeAddedEvent;
        MailSubscribeEvent.DeleteMailSubscribeEvent += HandleMailSubscribeDeletedEventAsync;
        Console.CancelKeyPress += DisposeAllServices;
    }

    public async Task InitAsync(IServiceProvider services, CancellationToken ct = default)
    {
        services.UseScheduler(scheduler =>
                scheduler
                    .ScheduleAsync(async () => await CheckAllAsync(ct))
                    .EverySeconds(5)
                    .PreventOverlapping("MailPlugin"))
            .OnError(ex => Log.Error(ex, "Exception from mail plugin: {msg}", ex.Message));
    }

    public async Task CheckAllAsync(CancellationToken ct = default)
    {
        List<MailSubscribeEntity> records = await _repo.GetAllAsync(true, ct);
        if (records.Count == 0)
        {
            Log.Debug("There isn't any mail subscribe yet!");
            return;
        }

        List<Task> tasks = new();
        foreach (MailSubscribeEntity record in records)
        {
            MailService? svc = _services.FirstOrDefault(s => s.Username == record.Address);
            if (svc == null)
            {
                svc = new MailService(
                    record.Host, record.Port, record.Ssl, record.Username, record.Password, record.Mailbox);
                _services.Add(svc);
            }

            tasks.Add(CheckAsync(svc, record, ct));
        }

        await Task.WhenAll(tasks);
    }

    private async Task CheckAsync(MailService svc, MailSubscribeEntity record, CancellationToken ct = default)
    {
        try
        {
            Mail mail = await svc.GetLatestMailAsync(ct);
            if (mail.Time <= record.LastMailTime)
            {
                Log.Debug("No new mail from the mail user {username}", record.Username);
                return;
            }

            async Task FnAsync(Mail mail)
            {
                await PushMessageAsync(mail, record, ct);

                record.LastMailTime = mail.Time;
                await _repo.SaveAsync(ct);
                Log.Debug("Succeeded to updated the mail user {username}'s record.", record.Username);
            }

            DateTime now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeUtil.CST);
            if (now.Hour is >= 0 and <= 5)
            {
                if (!_storedMails.ContainsKey(record.Username))
                    _storedMails[record.Username] = new Dictionary<DateTime, Mail>();
                if (!_storedMails[record.Username].ContainsKey(mail.Time))
                    _storedMails[record.Username][mail.Time] = mail;

                Log.Debug("Mail message of the user {username} is skipped because it's curfew time now.",
                    record.Username);
                return;
            }

            if (_storedMails.TryGetValue(record.Username, out Dictionary<DateTime, Mail>? storedMails)
                && storedMails.Count != 0)
            {
                List<DateTime> times = storedMails.Keys.ToList();
                times.Sort();
                foreach (DateTime time in times)
                {
                    await FnAsync(storedMails[time]);
                    _storedMails[record.Username].Remove(time);
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
            Log.Error(ex, "Failed to push mail message!\nAddress: {username}", record.Username);
        }
    }

    private async Task PushMessageAsync(Mail mail, MailSubscribeEntity record, CancellationToken ct = default)
    {
        (string title, string text, string detailText) = MailToStr(mail);

        List<MailSubscribeConfigEntity> configs = await _configRepo.GetAllAsync(record.Address, ct: ct);
        foreach (QQChannelSubscribeEntity channel in record.QQChannels)
        {
            if (!await QbSvc.ExistChannelAsync(channel.ChannelId))
            {
                Log.Warning("The channel {channelName}(id: {channelId}) doesn't exist!",
                    channel.ChannelName, channel.ChannelId);
                continue;
            }

            MailSubscribeConfigEntity config = configs.First(c => c.QQChannel.ChannelId == channel.ChannelId);
            if (config.PushToThread)
            {
                RichText richText = config.Detail
                    ? QQBotService.TextToRichText(detailText)
                    : QQBotService.TextToRichText(text);
                await QbSvc.PostThreadAsync(
                    channel.ChannelId, channel.ChannelName, title, JsonConvert.SerializeObject(richText), ct);
                Log.Information("Succeeded to push the mail message from the user {username}).", record.Username);
                continue;
            }

            if (config.Detail)
                await QbSvc.PushCommonMsgAsync(channel.ChannelId, channel.ChannelName, $"{title}\n\n{detailText}",
                    ct: ct);
            else
                await QbSvc.PushCommonMsgAsync(channel.ChannelId, channel.ChannelName, $"{title}\n\n{text}", ct: ct);
            Log.Information("Succeeded to push the mail message from the user {username}).", record.Username);
        }
    }

    private (string title, string text, string detailText) MailToStr(Mail mail)
    {
        string from = "";
        string to = "";
        foreach (AddressInfo item in mail.From)
            from += $"{item.Address} ";
        foreach (AddressInfo item in mail.To)
            to += $"{item.Address} ";

        string title = "【新邮件】来自 " + from;

        string text = mail.Text.AddRedirectToUrls();

        text = Regex.Replace(
            text,
            @"[A-Za-z0-9-_\u4e00-\u9fa5]+@[a-zA-Z0-9_-]+(\.[a-zA-Z0-9_-]+)+",
            match => match.ToString().Replace(".", "*"));

        string detailText = $"""
                             Subject: {mail.Subject}
                             To: {to}
                             Time: {mail.Time:yyyy-M-d H:mm}
                             ——————————
                             {mail.Text.AddRedirectToUrls()}
                             """;

        detailText = Regex.Replace(
            detailText,
            @"[A-Za-z0-9-_\u4e00-\u9fa5]+@[a-zA-Z0-9_-]+(\.[a-zA-Z0-9_-]+)+",
            match => match.ToString().Replace(".", "*"));

        return (title, text, detailText);
    }

    private void HandleMailSubscribeAddedEvent(
        string host, int port, bool usingSsl, string address, string password, string mailbox)
    {
        if (_services.FindIndex(s => s.Username == address) == -1)
            _services.Add(new MailService(host, port, usingSsl, address, password, mailbox));
    }

    private async Task HandleMailSubscribeDeletedEventAsync(string address, CancellationToken ct)
    {
        MailService? svc = _services.FirstOrDefault(s => s.Username == address);
        if (svc != null)
            try
            {
                await svc.DisconnectAsync(ct);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to disconnect the mail client!\nUsername: {username}", svc.Username);
            }
            finally
            {
                svc.Dispose();
                _services.Remove(svc);
            }
    }

    private void DisposeAllServices(object? sender, EventArgs e)
    {
        foreach (MailService svc in _services)
            svc.Dispose();
    }
}