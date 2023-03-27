using System.Text.RegularExpressions;
using Rabbitool.Event;
using Rabbitool.Model.DTO.Command;
using Rabbitool.Model.Entity.Subscribe;
using Rabbitool.Repository.Subscribe;
using Rabbitool.Service;
using Serilog;

namespace Rabbitool.Plugin.Command.Subscribe;

public class MailSubscribeCommandHandler
    : AbstractSubscribeCommandHandler<MailSubscribeEntity, MailSubscribeConfigEntity, MailSubscribeRepository, MailSubscribeConfigRepository>
{
    public MailSubscribeCommandHandler(
        QQBotService qbSvc,
        SubscribeDbContext dbCtx,
        QQChannelSubscribeRepository qsRepo,
        MailSubscribeRepository repo,
        MailSubscribeConfigRepository configRepo) : base(qbSvc, dbCtx, qsRepo, repo, configRepo)
    {
    }

    public override async Task<(string name, string? errMsg)> CheckId(string address, CancellationToken ct = default)
    {
        return Regex.IsMatch(address, @"^[a-zA-Z0-9_-]+@[a-zA-Z0-9_-]+(\.[a-zA-Z0-9_-]+)+$")
            ? (address, null)
            : ("", "错误：不合法的邮箱地址！");
    }

    public override async Task<string> Add(SubscribeCommandDTO cmd, CancellationToken ct = default)
    {
        if (cmd.SubscribeId == null)
            return $"请输入 {cmd.Platform} 对应的id！";
        (string address, string? errMsg) = await CheckId(cmd.SubscribeId, ct);
        if (errMsg != null)
            return errMsg;

        if (cmd.Configs == null)
            return "错误：需指定邮箱地址！";
        if (!cmd.Configs.TryGetValue("username", out string? username) || username == null)
            return "错误：需指定邮箱地址！";
        if (!cmd.Configs.TryGetValue("password", out string? password) || password == null)
            return "错误：需指定邮箱密码！";
        if (!cmd.Configs.TryGetValue("host", out string? host) || host == null)
            return "错误：需指定host！";
        if (!cmd.Configs.TryGetValue("port", out int? port) || port == null)
            return "错误：需指定port！";

        bool flag = true;

        MailSubscribeEntity? record = await _repo.GetOrDefaultAsync(address, ct: ct);
        if (record == null)
        {
            cmd.Configs.TryGetValue("mailbox", out string? mailbox);
            cmd.Configs.TryGetValue("ssl", out bool? ssl);
            record = new MailSubscribeEntity(
                username: username,
                address: address,
                password: password,
                host: host,
                port: (int)port,
                mailbox: mailbox ?? "INBOX",
                ssl: ssl ?? false
            );
            await _repo.AddAsync(record, ct);

            flag = false;
        }
        else if (record.QQChannels.Find(q => q.ChannelId == cmd.QQChannel.Id) != null)
        {
            flag = false;
        }

        (QQChannelSubscribeEntity channel, bool added) = await _qsRepo.AddSubscribeAsync(
            guildId: cmd.QQChannel.GuildId,
            guildName: cmd.QQChannel.GuildName,
            channelId: cmd.QQChannel.Id,
            channelName: cmd.QQChannel.Name,
            subscribe: record,
            ct: ct);
        await _configRepo.CreateOrUpdateAsync(channel, record, cmd.Configs, ct);

        await _dbCtx.SaveChangesAsync(ct);

        if (added && !flag)
        {
            MailSubscribeEvent.OnMailSubscribeAdded(
                record.Host, record.Port, record.Ssl, record.Address, record.Password, record.Mailbox);
            return $"成功：已添加订阅到 {cmd.QQChannel.Name} 子频道！";
        }
        else
        {
            return $"成功：已更新在 {cmd.QQChannel.Name} 子频道中的此订阅的配置！";
        }
    }

    public override async Task<string> Delete(SubscribeCommandDTO cmd, CancellationToken ct = default)
    {
        if (cmd.SubscribeId == null)
            return $"请输入 {cmd.Platform} 对应的id！";

        (_, string? errCommandMsg) = await CheckId(cmd.SubscribeId, ct);
        if (errCommandMsg != null)
            return errCommandMsg;

        try
        {
            MailSubscribeEntity subscribe = await _repo.GetAsync(cmd.SubscribeId, true, ct);
            QQChannelSubscribeEntity record = await _qsRepo.RemoveSubscribeAsync(cmd.QQChannel.Id, subscribe, ct);
            if (record.SubscribesAreAllEmpty())
                _qsRepo.Delete(record);

            await _repo.DeleteAsync(cmd.SubscribeId, ct);
            await _configRepo.DeleteAsync(cmd.QQChannel.Id, cmd.SubscribeId, ct);

            await _dbCtx.SaveChangesAsync(ct);
        }
        catch (InvalidOperationException iex)
        {
            Log.Error(iex, "The subscribe doesn't exist!\nInfo: {info}", new Dictionary<string, string>
            {
                { "type", nameof(MailSubscribeEntity) },
                { "subscribeId", cmd.SubscribeId },
                { "channelId", cmd.QQChannel.Id },
                { "channelName", cmd.QQChannel.Name }
            });
            return "错误：不存在该订阅！";
        }

        await MailSubscribeEvent.OnMailSubscribeDeletedAsync(cmd.SubscribeId, ct);

        return $"成功：已删除在 {cmd.QQChannel.Name} 子频道中的此订阅！";
    }
}
