using Autofac.Annotation;
using Autofac.Annotation.Condition;
using CodeHollow.FeedReader;
using Flurl.Http;
using Rabbitool.Api;
using Rabbitool.Model.Entity.Subscribe;
using Rabbitool.Repository.Subscribe;
using Serilog;

namespace Rabbitool.Plugin.Command.Subscribe.Handler;

[ConditionalOnProperty("youtube")]
[Component(AutofacScope = AutofacScope.SingleInstance)]
public class YoutubeSubscribeCommandHandler(
    QQBotApi qbSvc,
    SubscribeDbContext dbCtx,
    QQChannelSubscribeRepository qsRepo,
    YoutubeSubscribeRepository repo,
    YoutubeSubscribeConfigRepository configRepo)
    : AbstractSubscribeCommandHandler<YoutubeSubscribeEntity, YoutubeSubscribeConfigEntity, YoutubeSubscribeRepository,
        YoutubeSubscribeConfigRepository>(qbSvc, dbCtx, qsRepo, repo, configRepo)
{
    public override async Task<(string name, string? errMsg)> CheckId(string channelId, CancellationToken ct = default)
    {
        string resp;
        try
        {
            resp = await $"https://www.youtube.com/feeds/videos.xml?channel_id={channelId}"
                .GetStringAsync(cancellationToken: ct);
        }
        catch (FlurlHttpException ex)
        {
            if (ex.StatusCode != 404)
                throw;
            Log.Warning(ex, "The youtube user(channelId: {channelId} doesn't exist!", channelId);
            return ("", $"错误：channelId为 {channelId} 的用户在油管上不存在！");
        }

        Feed feed = FeedReader.ReadFromString(resp);
        return (feed.Items[0].Author, null);
    }
}