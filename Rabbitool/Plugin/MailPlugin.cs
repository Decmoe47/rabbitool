using Newtonsoft.Json;
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

    /// <summary>
    /// 会同时注册<see cref="MailSubscribeEvent.AddMailSubscribeEvent"/>
    /// 和<see cref="MailSubscribeEvent.DeleteMailSubscribeEvent"/>事件。
    /// </summary>
    public MailPlugin(
        QQBotService qbSvc,
        CosService cosSvc,
        string dbPath,
        string redirectUrl,
        string userAgent) : base(qbSvc, cosSvc, dbPath, redirectUrl, userAgent)
    {
        SubscribeDbContext dbCtx = new SubscribeDbContext(_dbPath);
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
            Log.Warning("There isn't any mail subscribe yet!");
            return;
        }

        List<Task> tasks = new();
        foreach (MailSubscribeEntity record in records)
        {
            MailService? svc = _services.FirstOrDefault(s => s.Address == record.Address);
            if (svc is not null)
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
            if (mail.Time > record.LastMailTime)
            {
                await PushMsgAsync(mail, record, cancellationToken);
                Log.Information("Succeeded to push the mail message from the user {address}).", record.Address);

                record.LastMailTime = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(mail.Time, "China Standard Time");
                await _repo.SaveAsync(cancellationToken);
                Log.Information("Succeeded to updated the mail user {address}'s record.", record.Address);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to push mail message!\nAddress: {address}", record.Address);
        }
    }

    private async Task PushMsgAsync(Mail mail, MailSubscribeEntity record, CancellationToken cancellationToken = default)
    {
        (string title, string text) = MailToStr(mail);

        List<MailSubscribeConfigEntity> configs = await _configRepo.GetAllAsync(
            record.Address, cancellationToken: cancellationToken);

        List<Task> tasks = new();
        foreach (QQChannelSubscribeEntity channel in record.QQChannels)
        {
            if (!await _qbSvc.ExistChannelAsync(channel.ChannelId))
            {
                Log.Warning("The channel {channelName}(id: {channelId}) doesn't exist!",
                    channel.ChannelName, channel.ChannelId);
                continue;
            }

            MailSubscribeConfigEntity? config = configs.FirstOrDefault(c => c.QQChannel.ChannelId == channel.ChannelId);
            if (config is not null)
            {
                if (config.PushToThread)
                {
                    List<Model.DTO.QQBot.Paragraph> richText = QQBotService.TextToParagraphs(text);
                    tasks.Add(_qbSvc.PostThreadAsync(channel.ChannelId, title, JsonConvert.SerializeObject(richText)));
                    continue;
                }
            }

            tasks.Add(_qbSvc.PushCommonMsgAsync(channel.ChannelId, $"{title}\n\n{text}"));
        }

        await Task.WhenAll(tasks);
    }

    private (string title, string text) MailToStr(Mail mail)
    {
        string from = "";
        string to = "";
        foreach (AddressInfo item in mail.From)
            from += $"{item.Address} ";
        foreach (AddressInfo item in mail.To)
            to += $"{item.Address} ";

        string title = "【新邮件】";
        string text = $@"From: {from}
To: {to}
Time: {TimeZoneInfo.ConvertTimeBySystemTimeZoneId(mail.Time, "China Standard Time"):yyyy-MM-dd HH:mm:ss zzz}
Subject: {mail.Subject}
——————————
{PluginHelper.AddRedirectToUrls(mail.Text, _redirectUrl)}";

        return (title, text);
    }

    private void HandleMailSubscribeAddedEvent(
        string host, int port, bool usingSsl, string address, string password, string mailbox)
    {
        if (_services.FindIndex(s => s.Address == address) == -1)
            _services.Add(new MailService(host, port, usingSsl, address, password, mailbox));
    }

    private async Task HandleMailSubscribeDeletedEventAsync(string address, CancellationToken cancellationToken)
    {
        MailService? svc = _services.FirstOrDefault(s => s.Address == address);
        if (svc is not null)
        {
            try
            {
                await svc.DisconnectAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to disconnect the mail client!\nAddress: {address}", svc.Address);
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
