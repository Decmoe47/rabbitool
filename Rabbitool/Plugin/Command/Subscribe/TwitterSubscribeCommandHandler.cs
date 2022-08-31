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
        string userAgent,
        SubscribeDbContext dbCtx,
        QQChannelSubscribeRepository qsRepo,
        TwitterSubscribeRepository repo,
        TwitterSubscribeConfigRepository configRepo) : base(qbSvc, userAgent, dbCtx, qsRepo, repo, configRepo)
    {
    }

    public override async Task<(string name, string? errCommandMsg)> CheckId(
        string screenName, CancellationToken cancellationToken = default)
    {
        _limiter.Wait();

        string resp;
        try
        {
            resp = await "https://api.twitter.com/1.1/statuses/user_timeline.json"
            .WithHeaders(new Dictionary<string, string>()
            {
                {"Authorization", _apiV1_1Auth},
                {"User-Agent", _userAgent},
            })
            .SetQueryParams(new Dictionary<string, string>()
            {
                {"screen_name", screenName},
                {"exclude_replies", "false"},
                {"include_rts", "true"},
            })
            .GetStringAsync(cancellationToken);
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
