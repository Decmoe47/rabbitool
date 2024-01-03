using Autofac.Annotation;
using Flurl.Http;
using Newtonsoft.Json.Linq;
using Rabbitool.Api;
using Rabbitool.Common.Configs;
using Rabbitool.Common.Extension;
using Rabbitool.Model.Entity.Subscribe;
using Rabbitool.Repository.Subscribe;
using Serilog;

namespace Rabbitool.Plugin.Command.Subscribe.Handler;

[Component(AutofacScope = AutofacScope.SingleInstance)]
public class BilibiliSubscribeCommandHandler(
    QQBotApi qbSvc,
    SubscribeDbContext dbCtx,
    QQChannelSubscribeRepository qsRepo,
    BilibiliSubscribeRepository repo,
    BilibiliSubscribeConfigRepository configRepo,
    CommonConfig commonConfig)
    : AbstractSubscribeCommandHandler<BilibiliSubscribeEntity, BilibiliSubscribeConfigEntity,
        BilibiliSubscribeRepository, BilibiliSubscribeConfigRepository>(qbSvc, dbCtx, qsRepo, repo, configRepo)
{
    private CookieJar? _jar;

    public override async Task<(string name, string? errMsg)> CheckId(string uid, CancellationToken ct = default)
    {
        if (!uint.TryParse(uid, out _))
        {
            Log.Warning("The uid {uid} can't be converted to uint!", uid);
            return ("", "错误：uid不正确！");
        }

        if (_jar == null)
        {
            _jar = new CookieJar();
            _ = await "https://bilibili.com"
                .WithCookies(_jar)
                .GetAsync(cancellationToken: ct);
        }

        string query = await BilibiliHelper.GenerateQueryAsync("mid", uid);
        string resp = await $"https://api.bilibili.com/x/space/wbi/acc/info?{query}"
            .WithCookies(_jar)
            .WithHeader("User-Agent", commonConfig.UserAgent)
            .GetStringAsync(cancellationToken: ct);

        JObject body = JObject.Parse(resp).RemoveNullAndEmptyProperties();

        string? name = (string?)body["data"]?["name"];
        if (name == null)
        {
            Log.Warning($"The bilibili user which uid is {uid} doesn't exist!", uid);
            return ("", $"错误：uid为 {uid} 的用户在b站上不存在!");
        }

        return (name, null);
    }
}