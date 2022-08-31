using CodeHollow.FeedReader;
using Flurl.Http;
using Rabbitool.Model.Entity.Subscribe;
using Rabbitool.Repository.Subscribe;
using Rabbitool.Service;
using Serilog;

namespace Rabbitool.Plugin.Command.Subscribe;

public class YoutubeSubscribeCommandHandler
    : AbstractSubscribeCommandHandler<YoutubeSubscribeEntity, YoutubeSubscribeConfigEntity, YoutubeSubscribeRepository, YoutubeSubscribeConfigRepository>
{
    public YoutubeSubscribeCommandHandler(
        QQBotService qbSvc,
        string userAgent,
        SubscribeDbContext dbCtx,
        QQChannelSubscribeRepository qsRepo,
        YoutubeSubscribeRepository repo,
        YoutubeSubscribeConfigRepository configRepo) : base(qbSvc, userAgent, dbCtx, qsRepo, repo, configRepo)
    {
    }

    public override async Task<(string name, string? errCommandMsg)> CheckId(
        string channelId, CancellationToken cancellationToken = default)
    {
        _limiter.Wait();

        string resp;
        try
        {
            resp = await $"https://www.youtube.com/feeds/videos.xml?channel_id={channelId}"
                .WithHeader("User-Agent", _userAgent)
                .GetStringAsync();
        }
        catch (FlurlHttpException ex)
        {
            if (ex.StatusCode == 404)
            {
                Log.Warning(ex, "The youtube user(channelId: {channelId} doesn't exist!", channelId);
                return ("", $"错误：channelId为 {channelId} 的用户在油管上不存在！");
            }
            else
            {
                throw ex;
            }
        }

        Feed feed = FeedReader.ReadFromString(resp);
        return (feed.Items[0].Author, null);
    }
}
