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

    public override async Task<(string name, string? errCommandMsg)> CheckId(
        string address, CancellationToken cancellationToken = default)
    {
        return Regex.IsMatch(address, @"^[a-zA-Z0-9_-]+@[a-zA-Z0-9_-]+(\.[a-zA-Z0-9_-]+)+$")
            ? (address, null)
            : ("", "错误：不合法的邮箱地址！");
    }

#pragma warning restore CS1998 // 异步方法缺少 "await" 运算符，将以同步方式运行

    public override async Task<string> Add(SubscribeCommandDTO command, CancellationToken cancellationToken = default)
    {
        if (command.SubscribeId is null)
            return $"请输入 {command.Platform} 对应的id！";
        (string username, string? errCommandMsg) = await CheckId(command.SubscribeId, cancellationToken);
        if (errCommandMsg is not null)
            return errCommandMsg;

        if (command.Configs is null)
            return "错误：需指定邮箱地址！";
        if (!command.Configs.TryGetValue("address", out string? address) || address is null)
            return "错误：需指定邮箱地址！";
        if (!command.Configs.TryGetValue("password", out string? password) || password is null)
            return "错误：需指定邮箱密码！";
        if (!command.Configs.TryGetValue("host", out string? host) || host is null)
            return "错误：需指定host！";
        if (!command.Configs.TryGetValue("port", out int? port) || port is null)
            return "错误：需指定port！";

        bool flag = true;
        bool added;
        MailSubscribeEntity record;
        QQChannelSubscribeEntity channel;
        Dictionary<string, string> logInfo = new()
        {
            { "type", nameof(YoutubeSubscribeEntity) },
            { "subscribeId", command.SubscribeId },
            { "channelId", command.QQChannel.Id },
            { "channelName", command.QQChannel.Name }
        };

        MailSubscribeEntity? entity = await _repo.GetOrDefaultAsync(address, cancellationToken: cancellationToken);
        if (entity is null)
        {
            command.Configs.TryGetValue("mailbox", out dynamic? mailbox);
            command.Configs.TryGetValue("ssl", out dynamic? ssl);
            entity = new MailSubscribeEntity(
                username: username,
                address: address,
                password: command.Configs["password"],
                host: command.Configs["host"],
                port: command.Configs["port"],
                mailbox: mailbox ?? "INBOX",
                ssl: ssl ?? false
            );
            await _repo.AddAsync(entity, cancellationToken);

            flag = false;
        }
        else if (entity.ContainsQQChannel(command.QQChannel.Id))
        {
            flag = false;
        }
        record = entity;

        (channel, added) = await _qsRepo.AddSubscribeAsync(
            command.QQChannel.GuildId, command.QQChannel.Id, command.QQChannel.Name, record, cancellationToken);
        await _configRepo.CreateOrUpdateAsync(channel, record, command.Configs, cancellationToken);

        await _dbCtx.SaveChangesAsync(cancellationToken);

        if (added && !flag)
        {
            MailSubscribeEvent.OnMailSubscribeAdded(
                record.Host, record.Port, record.Ssl, address, record.Password, record.Mailbox);
            return $"成功：已添加订阅到 {command.QQChannel.Name} 子频道！";
        }
        else
        {
            return $"成功：已更新在 {command.QQChannel.Name} 子频道中的此订阅的配置！";
        }
    }

    public override async Task<string> Delete(SubscribeCommandDTO command, CancellationToken cancellationToken = default)
    {
        if (command.SubscribeId is null)
            return $"请输入 {command.Platform} 对应的id！";

        (_, string? errCommandMsg) = await CheckId(command.SubscribeId, cancellationToken);
        if (errCommandMsg is not null)
            return errCommandMsg;

        Dictionary<string, string> logInfo = new()
        {
            { "type", nameof(MailSubscribeEntity) },
            { "subscribeId", command.SubscribeId },
            { "channelId", command.QQChannel.Id },
            { "channelName", command.QQChannel.Name }
        };

        try
        {
            QQChannelSubscribeEntity record = await _qsRepo.RemoveSubscribeAsync(
                command.QQChannel.Id, command.SubscribeId, e => e.MailSubscribes, cancellationToken);
            if (record.SubscribesAreAllEmpty())
                _qsRepo.Delete(record);

            await _repo.DeleteAsync(command.SubscribeId, cancellationToken);
            await _configRepo.DeleteAsync(command.QQChannel.Id, command.SubscribeId, cancellationToken);

            await _dbCtx.SaveChangesAsync(cancellationToken);
        }
        catch (InvalidOperationException iex)
        {
            Log.Error(iex, "The subscribe doesn't exist!\nInfo: {info}", logInfo);
            return "错误：不存在该订阅！";
        }

        await MailSubscribeEvent.OnMailSubscribeDeletedAsync(command.SubscribeId, cancellationToken);

        return $"成功：已删除在 {command.QQChannel.Name} 子频道中的此订阅！";
    }
}
