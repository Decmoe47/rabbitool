using Autofac.Annotation;
using Flurl.Http;
using Newtonsoft.Json.Linq;
using Rabbitool.Common.Configs;
using Rabbitool.Model.Entity.Subscribe;
using Rabbitool.Repository.Subscribe;
using Serilog;

namespace Rabbitool.Plugin.Command.Subscribe.Handler;

[Component]
public class TwitterSubscribeCommandHandler(
    SubscribeDbContext dbCtx,
    QQChannelSubscribeRepository qsRepo,
    TwitterSubscribeRepository repo,
    TwitterSubscribeConfigRepository configRepo,
    TwitterConfig twitterConfig)
    : AbstractSubscribeCommandHandler<TwitterSubscribeEntity, TwitterSubscribeConfigEntity, TwitterSubscribeRepository,
        TwitterSubscribeConfigRepository>(dbCtx, qsRepo, repo, configRepo)
{
    public override async Task<(string name, string? errMsg)> CheckId(string screenName, CancellationToken ct = default)
    {
        string resp;
        try
        {
            if (twitterConfig is { XCsrfToken: not null, Cookie: not null })
                resp = await "https://api.twitter.com/1.1/statuses/user_timeline.json"
                    .WithTimeout(10)
                    .WithOAuthBearerToken(twitterConfig.BearerToken)
                    .SetQueryParams(new Dictionary<string, string>
                    {
                        { "count", "5" },
                        { "screen_name", screenName },
                        { "exclude_replies", "true" },
                        { "include_rts", "true" },
                        {
                            "tweet_mode", "extended"
                        } // https://stackoverflow.com/questions/38717816/twitter-api-text-field-value-is-truncated
                    })
                    .WithHeaders(new Dictionary<string, string>
                    {
                        { "x-csrf-token", twitterConfig.XCsrfToken },
                        { "Cookie", twitterConfig.Cookie }
                    })
                    .GetStringAsync(cancellationToken: ct);
            else
                resp = await "https://api.twitter.com/1.1/statuses/user_timeline.json"
                    .WithTimeout(10)
                    .WithOAuthBearerToken(twitterConfig.BearerToken)
                    .SetQueryParams(new Dictionary<string, string>
                    {
                        { "count", "5" },
                        { "screen_name", screenName },
                        { "exclude_replies", "true" },
                        { "include_rts", "true" },
                        { "tweet_mode", "extended" }
                    })
                    .GetStringAsync(cancellationToken: ct);
        }
        catch (FlurlHttpException ex)
        {
            if (ex.StatusCode != 404)
                throw;
            Log.Warning(ex, "The twitter user(screenName: {screenName} doesn't exist!", screenName);
            return ("", $"错误：screenName为 {screenName} 的用户在推特上不存在！");
        }

        JArray body = JArray.Parse(resp);
        return ((string)body[0]["user"]!["name"]!, null);
    }
}