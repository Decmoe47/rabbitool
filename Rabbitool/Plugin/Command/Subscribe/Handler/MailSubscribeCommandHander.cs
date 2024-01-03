using System.Text.RegularExpressions;
using Autofac.Annotation;
using Autofac.Annotation.Condition;
using Rabbitool.Event;
using Rabbitool.Model.DTO.Command;
using Rabbitool.Model.Entity.Subscribe;
using Rabbitool.Repository.Subscribe;
using Serilog;

namespace Rabbitool.Plugin.Command.Subscribe.Handler;

[ConditionalOnProperty("mail")]
[Component]
public partial class MailSubscribeCommandHandler(
    SubscribeDbContext dbCtx,
    QQChannelSubscribeRepository qsRepo,
    MailSubscribeRepository repo,
    MailSubscribeConfigRepository configRepo)
    : AbstractSubscribeCommandHandler<MailSubscribeEntity, MailSubscribeConfigEntity, MailSubscribeRepository,
        MailSubscribeConfigRepository>(dbCtx, qsRepo, repo, configRepo)
{
    public override Task<(string name, string? errMsg)> CheckId(string address, CancellationToken ct = default)
    {
        return Task.FromResult(MyRegex1().IsMatch(address)
            ? (address, null)
            : ("", "错误：不合法的邮箱地址！"));
    }

    public override async Task<string> Add(SubscribeCommand cmd, CancellationToken ct = default)
    {
        if (cmd.SubscribeId == null)
            return $"请输入 {cmd.Platform} 对应的id！";

        (string address, _) = await CheckId(cmd.SubscribeId, ct);

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

        MailSubscribeEntity? record = await repo.GetOrDefaultAsync(address, ct: ct);
        if (record == null)
        {
            cmd.Configs.TryGetValue("mailbox", out string? mailbox);
            cmd.Configs.TryGetValue("ssl", out bool? ssl);
            record = new MailSubscribeEntity(
                username,
                address,
                password,
                host,
                (int)port,
                mailbox ?? "INBOX",
                ssl ?? false
            );
            await repo.AddAsync(record, ct);

            flag = false;
        }
        else if (record.QQChannels.Find(q => q.ChannelId == cmd.QQChannel.Id) != null)
        {
            flag = false;
        }

        (QQChannelSubscribeEntity channel, bool added) = await qsRepo.AddSubscribeAsync(
            cmd.QQChannel.GuildId,
            cmd.QQChannel.GuildName,
            cmd.QQChannel.Id,
            cmd.QQChannel.Name,
            record,
            ct);
        await configRepo.CreateOrUpdateAsync(channel, record, cmd.Configs, ct);

        await dbCtx.SaveChangesAsync(ct);

        if (added && !flag)
        {
            MailSubscribeEvent.OnMailSubscribeAdded(
                record.Host, record.Port, record.Ssl, record.Address, record.Password, record.Mailbox);
            return $"成功：已添加订阅到 {cmd.QQChannel.Name} 子频道！";
        }

        return $"成功：已更新在 {cmd.QQChannel.Name} 子频道中的此订阅的配置！";
    }

    public override async Task<string> Delete(SubscribeCommand cmd, CancellationToken ct = default)
    {
        if (cmd.SubscribeId == null)
            return $"请输入 {cmd.Platform} 对应的id！";

        try
        {
            MailSubscribeEntity subscribe = await repo.GetAsync(cmd.SubscribeId, true, ct);
            QQChannelSubscribeEntity record = await qsRepo.RemoveSubscribeAsync(cmd.QQChannel.Id, subscribe, ct);
            if (record.SubscribesAreAllEmpty())
                qsRepo.Delete(record);

            await repo.DeleteAsync(cmd.SubscribeId, ct);
            await configRepo.DeleteAsync(cmd.QQChannel.Id, cmd.SubscribeId, ct);

            await dbCtx.SaveChangesAsync(ct);
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

    [GeneratedRegex(@"^[a-zA-Z0-9_-]+@[a-zA-Z0-9_-]+(\.[a-zA-Z0-9_-]+)+$")]
    private static partial Regex MyRegex1();
}