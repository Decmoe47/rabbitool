using Flurl;
using Flurl.Http;
using Newtonsoft.Json.Linq;
using Rabbitool.Common.Extension;
using Rabbitool.Model.Entity.Subscribe;
using Rabbitool.Repository.Subscribe;
using Rabbitool.Service;
using Serilog;

namespace Rabbitool.Plugin.Command.Subscribe;

public class BilibiliSubscribeCommandHandler
    : AbstractSubscribeCommandHandler<BilibiliSubscribeEntity, BilibiliSubscribeConfigEntity, BilibiliSubscribeRepository, BilibiliSubscribeConfigRepository>
{
    private CookieJar? _jar;

    public BilibiliSubscribeCommandHandler(
        QQBotService qbSvc,
        string userAgent,
        SubscribeDbContext dbCtx,
        QQChannelSubscribeRepository qsRepo,
        BilibiliSubscribeRepository repo,
        BilibiliSubscribeConfigRepository configRepo) : base(qbSvc, userAgent, dbCtx, qsRepo, repo, configRepo)
    {
    }

    public override async Task<(string name, string? errMsg)> CheckId(string uid, CancellationToken ct = default)
    {
        _limiter.Wait(ct: ct);

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
                    .GetAsync();
        }

        string resp = await "https://api.bilibili.com/x/space/acc/info"
                .SetQueryParam("mid", uid)
                .WithCookies(_jar)
                .WithHeader("User-Agent", _userAgent)
                .GetStringAsync(ct);

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
