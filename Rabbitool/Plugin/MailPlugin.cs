using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Rabbitool.Common.Util;
using Rabbitool.Event;
using Rabbitool.Model.DTO.Mail;
using Rabbitool.Model.Entity.Subscribe;
using Rabbitool.Repository.Subscribe;
using Rabbitool.Service;
using Serilog;

namespace Rabbitool.Plugin;

public class MailPlugin : BasePlugin
{
    private readonly List<MailService> _services = new();
    private readonly MailSubscribeRepository _repo;
    private readonly MailSubscribeConfigRepository _configRepo;

    private Dictionary<string, Dictionary<DateTime, Mail>> _storedMails = new();

    /// <summary>
    /// 会同时注册<see cref="MailSubscribeEvent.AddMailSubscribeEvent"/>
    /// 和<see cref="MailSubscribeEvent.DeleteMailSubscribeEvent"/>
    /// 和<see cref="AppDomain.CurrentDomain.ProcessExit"/>事件。
    /// </summary>
    public MailPlugin(
        QQBotService qbSvc,
        CosService cosSvc,
        string dbPath,
        string redirectUrl,
        string userAgent) : base(qbSvc, cosSvc, dbPath, redirectUrl, userAgent)
    {
        SubscribeDbContext dbCtx = new(_dbPath);
        _repo = new MailSubscribeRepository(dbCtx);
        _configRepo = new MailSubscribeConfigRepository(dbCtx);

        MailSubscribeEvent.AddMailSubscribeEvent += HandleMailSubscribeAddedEvent;
        MailSubscribeEvent.DeleteMailSubscribeEvent += HandleMailSubscribeDeletedEventAsync;
        AppDomain.CurrentDomain.ProcessExit += DisposeAllServices;
    }

    public async Task CheckAllAsync(CancellationToken cancellationToken = default)
    {
        List<MailSubscribeEntity> records = await _repo.GetAllAsync(true, cancellationToken);
        if (records.Count == 0)
        {
            Log.Debug("There isn't any mail subscribe yet!");
            return;
        }

        List<Task> tasks = new();
        foreach (MailSubscribeEntity record in records)
        {
            MailService? svc = _services.FirstOrDefault(s => s.Username == record.Address);
            if (svc is null)
            {
                svc = new MailService(record.Host, record.Port, record.Ssl, record.Username, record.Password, record.Mailbox);
                _services.Add(svc);
            }

            tasks.Add(CheckAsync(svc, record, cancellationToken));
        }
        await Task.WhenAll(tasks);
    }

    private async Task CheckAsync(
        MailService svc, MailSubscribeEntity record, CancellationToken cancellationToken = default)
    {
        try
        {
            Mail mail = await svc.GetLatestMailAsync(cancellationToken);
            if (mail.Time <= record.LastMailTime)
            {
                Log.Debug("No new mail from the mail user {username}", record.Username);
                return;
            }

            async Task FnAsync(Mail mail)
            {
                bool pushed = await PushMsgAsync(mail, record, cancellationToken);
                if (pushed)
                    Log.Information("Succeeded to push the mail message from the user {username}).", record.Username);

                record.LastMailTime = mail.Time;
                await _repo.SaveAsync(cancellationToken);
                Log.Debug("Succeeded to updated the mail user {username}'s record.", record.Username);
            };

            DateTime now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeUtil.CST);
            if (now.Hour >= 0 && now.Hour <= 6)
            {
                if (!_storedMails.ContainsKey(record.Username) || !_storedMails[record.Username].ContainsKey(mail.Time))
                    _storedMails[record.Username][mail.Time] = mail;
                Log.Debug("Mail message of the user {userrname} is skipped because it's curfew time now.",
                    record.Username);
                return;
            }

            if (_storedMails.TryGetValue(record.Username, out Dictionary<DateTime, Mail>? storedMails) && storedMails != null && storedMails.Count != 0)
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
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to push mail message!\nAddress: {username}", record.Username);
        }
    }

    private async Task<bool> PushMsgAsync(Mail mail, MailSubscribeEntity record, CancellationToken cancellationToken = default)
    {
        (string title, string text, string detailText) = MailToStr(mail);

        List<MailSubscribeConfigEntity> configs = await _configRepo.GetAllAsync(
            record.Address, cancellationToken: cancellationToken);

        bool pushed = false;
        List<Task> tasks = new();
        foreach (QQChannelSubscribeEntity channel in record.QQChannels)
        {
            if (!await _qbSvc.ExistChannelAsync(channel.ChannelId))
            {
                Log.Warning("The channel {channelName}(id: {channelId}) doesn't exist!",
                    channel.ChannelName, channel.ChannelId);
                continue;
            }

            MailSubscribeConfigEntity config = configs.First(c => c.QQChannel.ChannelId == channel.ChannelId);
            if (config.PushToThread)
            {
                List<Model.DTO.QQBot.Paragraph> richText = config.Detail
                    ? QQBotService.TextToParagraphs(detailText)
                    : QQBotService.TextToParagraphs(text);
                tasks.Add(_qbSvc.PostThreadAsync(channel.ChannelId, title, JsonConvert.SerializeObject(richText)));
                pushed = true;
                continue;
            }

            if (config.Detail)
                tasks.Add(_qbSvc.PushCommonMsgAsync(channel.ChannelId, $"{title}\n\n{detailText}"));
            else
                tasks.Add(_qbSvc.PushCommonMsgAsync(channel.ChannelId, $"{title}\n\n{text}"));

            pushed = true;
        }

        await Task.WhenAll(tasks);
        return pushed;
    }

    private (string title, string text, string detailText) MailToStr(Mail mail)
    {
        string from = "";
        string to = "";
        foreach (AddressInfo item in mail.From)
            from += $"{item.Address} ";
        foreach (AddressInfo item in mail.To)
            to += $"{item.Address} ";

        string title = "【新邮件】";

        string text = mail.Text.AddRedirectToUrls(_redirectUrl);

        text = Regex.Replace(
                text,
                @"[A-Za-z0-9-_\u4e00-\u9fa5]+@[a-zA-Z0-9_-]+(\.[a-zA-Z0-9_-]+)+",
                new MatchEvaluator((match) => match.ToString().Replace(".", "*")));

        string detailText = $@"From: {from}
To: {to}
Time: {TimeZoneInfo.ConvertTimeFromUtc(mail.Time, TimeUtil.CST):yyyy-MM-dd HH:mm:ss zzz}
Subject: {mail.Subject}
——————————
{mail.Text.AddRedirectToUrls(_redirectUrl)}";

        detailText = Regex.Replace(
                detailText,
                @"[A-Za-z0-9-_\u4e00-\u9fa5]+@[a-zA-Z0-9_-]+(\.[a-zA-Z0-9_-]+)+",
                new MatchEvaluator((match) => match.ToString().Replace(".", "*")));

        return (title, text, detailText);
    }

    private void HandleMailSubscribeAddedEvent(
        string host, int port, bool usingSsl, string address, string password, string mailbox)
    {
        if (_services.FindIndex(s => s.Username == address) == -1)
            _services.Add(new MailService(host, port, usingSsl, address, password, mailbox));
    }

    private async Task HandleMailSubscribeDeletedEventAsync(string address, CancellationToken cancellationToken)
    {
        MailService? svc = _services.FirstOrDefault(s => s.Username == address);
        if (svc is not null)
        {
            try
            {
                await svc.DisconnectAsync(cancellationToken);
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
    }

    private void DisposeAllServices(object? sender, EventArgs e)
    {
        foreach (MailService svc in _services)
            svc.Dispose();
    }
}
