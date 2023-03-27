using Flurl;
using Flurl.Http;
using Newtonsoft.Json.Linq;
using Rabbitool.Model.Entity.Subscribe;
using Rabbitool.Repository.Subscribe;
using Rabbitool.Service;
using Serilog;

namespace Rabbitool.Plugin.Command.Subscribe;

public class TwitterSubscribeCommandHandler
    : AbstractSubscribeCommandHandler<TwitterSubscribeEntity, TwitterSubscribeConfigEntity, TwitterSubscribeRepository, TwitterSubscribeConfigRepository>
{
    private readonly string _apiV1_1Auth = "Bearer AAAAAAAAAAAAAAAAAAAAANRILgAAAAAAnNwIzUejRCOuH5E6I8xnZz4puTs%3D1Zv7ttfk8LF81IUq16cHjhLTvJu4FA33AGWWjCpTnA";

    public TwitterSubscribeCommandHandler(
        QQBotService qbSvc,
        SubscribeDbContext dbCtx,
        QQChannelSubscribeRepository qsRepo,
        TwitterSubscribeRepository repo,
        TwitterSubscribeConfigRepository configRepo) : base(qbSvc, dbCtx, qsRepo, repo, configRepo)
    {
    }

    public override async Task<(string name, string? errMsg)> CheckId(string screenName, CancellationToken ct = default)
    {
        string resp;
        try
        {
            resp = await "https://api.twitter.com/1.1/statuses/user_timeline.json"
            .WithHeader("Authorization", _apiV1_1Auth)
            .SetQueryParams(new Dictionary<string, string>()
            {
                {"screen_name", screenName},
                {"exclude_replies", "false"},
                {"include_rts", "true"},
                {"count", "5"}
            })
            .GetStringAsync(ct);
        }
        catch (FlurlHttpException ex)
        {
            if (ex.StatusCode == 404)
            {
                Log.Warning(ex, "The twitter user(screenName: {screenName} doesn't exist!", screenName);
                return ("", $"错误：screenName为 {screenName} 的用户在推特上不存在！");
            }
            else
            {
                throw ex;
            }
        }

        JArray body = JArray.Parse(resp);
        return ((string)body[0]!["user"]!["name"]!, null);
    }
}
