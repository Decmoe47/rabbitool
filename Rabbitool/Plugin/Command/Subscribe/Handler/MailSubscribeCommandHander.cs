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
        string userAgent,
        SubscribeDbContext dbCtx,
        QQChannelSubscribeRepository qsRepo,
        MailSubscribeRepository repo,
        MailSubscribeConfigRepository configRepo) : base(qbSvc, userAgent, dbCtx, qsRepo, repo, configRepo)
    {
    }

#pragma warning disable CS1998 // 异步方法缺少 "await" 运算符，将以同步方式运行

    public override async Task<(string name, string? errMsg)> CheckId(string address, CancellationToken ct = default)
    {
        return Regex.IsMatch(address, @"^[a-zA-Z0-9_-]+@[a-zA-Z0-9_-]+(\.[a-zA-Z0-9_-]+)+$")
            ? (address, null)
            : ("", "错误：不合法的邮箱地址！");
    }

#pragma warning restore CS1998 // 异步方法缺少 "await" 运算符，将以同步方式运行

    public override async Task<string> Add(SubscribeCommandDTO cmd, CancellationToken ct = default)
    {
        if (cmd.SubscribeId is null)
            return $"请输入 {cmd.Platform} 对应的id！";
        (string username, string? errCommandMsg) = await CheckId(cmd.SubscribeId, ct);
        if (errCommandMsg is not null)
            return errCommandMsg;

        if (cmd.Configs is null)
            return "错误：需指定邮箱地址！";
        if (!cmd.Configs.TryGetValue("address", out string? address) || address is null)
            return "错误：需指定邮箱地址！";
        if (!cmd.Configs.TryGetValue("password", out string? password) || password is null)
            return "错误：需指定邮箱密码！";
        if (!cmd.Configs.TryGetValue("host", out string? host) || host is null)
            return "错误：需指定host！";
        if (!cmd.Configs.TryGetValue("port", out int? port) || port is null)
            return "错误：需指定port！";

        bool flag = true;

        MailSubscribeEntity? record = await _repo.GetOrDefaultAsync(address, ct: ct);
        if (record is null)
        {
            cmd.Configs.TryGetValue("mailbox", out dynamic? mailbox);
            cmd.Configs.TryGetValue("ssl", out dynamic? ssl);
            record = new MailSubscribeEntity(
                username: username,
                address: address,
                password: cmd.Configs["password"],
                host: cmd.Configs["host"],
                port: cmd.Configs["port"],
                mailbox: mailbox ?? "INBOX",
                ssl: ssl ?? false
            );
            await _repo.AddAsync(record, ct);

            flag = false;
        }
        else if (record.ContainsQQChannel(cmd.QQChannel.Id))
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
                record.Host, record.Port, record.Ssl, address, record.Password, record.Mailbox);
            return $"成功：已添加订阅到 {cmd.QQChannel.Name} 子频道！";
        }
        else
        {
            return $"成功：已更新在 {cmd.QQChannel.Name} 子频道中的此订阅的配置！";
        }
    }

    public override async Task<string> Delete(SubscribeCommandDTO command, CancellationToken ct = default)
    {
        if (command.SubscribeId is null)
            return $"请输入 {command.Platform} 对应的id！";

        (_, string? errCommandMsg) = await CheckId(command.SubscribeId, ct);
        if (errCommandMsg is not null)
            return errCommandMsg;

        try
        {
            QQChannelSubscribeEntity record = await _qsRepo.RemoveSubscribeAsync(
                command.QQChannel.Id, command.SubscribeId, e => e.MailSubscribes, ct);
            if (record.SubscribesAreAllEmpty())
                _qsRepo.Delete(record);

            await _repo.DeleteAsync(command.SubscribeId, ct);
            await _configRepo.DeleteAsync(command.QQChannel.Id, command.SubscribeId, ct);

            await _dbCtx.SaveChangesAsync(ct);
        }
        catch (InvalidOperationException iex)
        {
            Log.Error(iex, "The subscribe doesn't exist!\nInfo: {info}", new Dictionary<string, string>
            {
                { "type", nameof(MailSubscribeEntity) },
                { "subscribeId", command.SubscribeId },
                { "channelId", command.QQChannel.Id },
                { "channelName", command.QQChannel.Name }
            });
            return "错误：不存在该订阅！";
        }

        await MailSubscribeEvent.OnMailSubscribeDeletedAsync(command.SubscribeId, ct);

        return $"成功：已删除在 {command.QQChannel.Name} 子频道中的此订阅！";
    }
}
