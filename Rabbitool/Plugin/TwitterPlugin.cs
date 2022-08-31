using Newtonsoft.Json;
using Rabbitool.Model.DTO.QQBot;
using Rabbitool.Model.DTO.Twitter;
using Rabbitool.Model.Entity.Subscribe;
using Rabbitool.Repository.Subscribe;
using Rabbitool.Service;
using Serilog;

namespace Rabbitool.Plugin;

public class TwitterPlugin : BasePlugin
{
    private readonly TwitterService _svc;
    private readonly TwitterSubscribeRepository _repo;
    private readonly TwitterSubscribeConfigRepository _configRepo;

    public TwitterPlugin(
        QQBotService qbSvc,
        CosService cosSvc,
        string dbPath,
        string redirectUrl,
        string userAgent) : base(qbSvc, cosSvc, dbPath, redirectUrl, userAgent)
    {
        _svc = new TwitterService(userAgent);

        SubscribeDbContext dbCtx = new(_dbPath);
        _repo = new TwitterSubscribeRepository(dbCtx);
        _configRepo = new TwitterSubscribeConfigRepository(dbCtx);
    }

    public TwitterPlugin(
        string apiV2Token,
        QQBotService qbSvc,
        CosService cosSvc,
        string dbPath,
        string redirectUrl,
        string userAgent) : base(qbSvc, cosSvc, dbPath, redirectUrl, userAgent)
    {
        _svc = new TwitterService(userAgent, apiV2Token);

        SubscribeDbContext dbCtx = new(_dbPath);
        _repo = new TwitterSubscribeRepository(dbCtx);
        _configRepo = new TwitterSubscribeConfigRepository(dbCtx);
    }

    public TwitterPlugin(
        string xCsrfToken,
        string cookie,
        QQBotService qbSvc,
        CosService cosSvc,
        string dbPath,
        string redirectUrl,
        string userAgent) : base(qbSvc, cosSvc, dbPath, redirectUrl, userAgent)
    {
        _svc = new TwitterService(userAgent, xCsrfToken, cookie);

        SubscribeDbContext dbCtx = new(_dbPath);
        _repo = new TwitterSubscribeRepository(dbCtx);
        _configRepo = new TwitterSubscribeConfigRepository(dbCtx);
    }

    public async Task CheckAllAsync(CancellationToken cancellationToken = default)
    {
        List<TwitterSubscribeEntity> records = await _repo.GetAllAsync(true, cancellationToken);
        if (records.Count == 0)
        {
            Log.Warning("There isn't any twitter subscribe yet!");
            return;
        }

        List<Task> tasks = new();
        foreach (TwitterSubscribeEntity record in records)
            tasks.Add(CheckAsync(record, cancellationToken));
        await Task.WhenAll(tasks);
    }

    private async Task CheckAsync(TwitterSubscribeEntity record, CancellationToken cancellationToken = default)
    {
        try
        {
            Tweet tweet = await _svc.GetLatestTweetAsync(record.ScreenName, cancellationToken);
            if (tweet.PubTime > record.LastTweetTime)
            {
                await PushTweetAsync(tweet, record, cancellationToken);
                Log.Information("Succeeded to push the tweet message from the user {name}(screenName: {screenName}).",
                        tweet.Author, tweet.AuthorScreenName);

                record.LastTweetTime = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(tweet.PubTime, "China Standard Time");
                record.LastTweetId = tweet.Id;
                await _repo.SaveAsync(cancellationToken);
                Log.Information("Succeeded to updated the twitter user {name}(screenName: {screenName})'s record.",
                        tweet.Author, tweet.AuthorScreenName);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to push tweet message!\nName: {name}\nScreenName: {screenName}",
                record.Name, record.ScreenName);
        }
    }

    private async Task PushTweetAsync(
        Tweet tweet, TwitterSubscribeEntity subscribe, CancellationToken cancellationToken = default)
    {
        (string title, string text) = await TweetToStrAsync(tweet, cancellationToken);
        List<Paragraph> richText = await TweetToRichTextAsync(tweet, text, cancellationToken);
        List<string> imgUrls = await GetTweetImgUrlsAsync(tweet, cancellationToken);

        List<TwitterSubscribeConfigEntity> configs = await _configRepo.GetAllAsync(
            subscribe.ScreenName, cancellationToken: cancellationToken);

        List<Task> tasks = new();
        foreach (QQChannelSubscribeEntity channel in subscribe.QQChannels)
        {
            if (!await _qbSvc.ExistChannelAsync(channel.ChannelId))
            {
                Log.Warning("The channel {channelName}(id: {channelId}) doesn't exist!",
                    channel.ChannelName, channel.ChannelId);
                continue;
            }

            TwitterSubscribeConfigEntity? config = configs.FirstOrDefault(c => c.QQChannel.ChannelId == channel.ChannelId);
            if (config is not null)
            {
                if (tweet.Origin is not null)
                {
                    if (config.RtPush is false) continue;
                    if (tweet.Type == TweetTypeEnum.RT && !config.PureRtPush) continue;
                }
                if (config.PushToThread)
                {
                    tasks.Add(_qbSvc.PostThreadAsync(channel.ChannelId, title, JsonConvert.SerializeObject(richText)));
                    continue;
                }
            }

            tasks.Add(_qbSvc.PushCommonMsgAsync(channel.ChannelId, $"{title}\n\n{text}", imgUrls));
        }

        await Task.WhenAll(tasks);
    }

    private async Task<(string title, string text)> TweetToStrAsync(
        Tweet tweet, CancellationToken cancellationToken = default)
    {
        string title;
        string text;
        string pubTimeStr = TimeZoneInfo
            .ConvertTimeBySystemTimeZoneId(tweet.PubTime, "China Standard Time")
            .ToString("yyyy-MM-dd HH:mm:ss zzz");

        if (tweet.Origin is null)
        {
            title = $"【新推文】来自 {tweet.Author}";
            text = $@"{PluginHelper.AddRedirectToUrls(tweet.Text, _redirectUrl)}
——————————
推文发布时间：{pubTimeStr}
推文链接：{PluginHelper.AddRedirectToUrls(tweet.Url, _redirectUrl)}";
        }
        else if (tweet.Type == TweetTypeEnum.Quote)
        {
            string originPubTimeStr = TimeZoneInfo
                .ConvertTimeBySystemTimeZoneId(tweet.Origin.PubTime, "China Standard Time")
                .ToString("yyyy-MM-dd HH:mm:ss zzz");

            title = $"【新带评论转发推文】来自 {tweet.Author}";
            text = $@"{PluginHelper.AddRedirectToUrls(tweet.Text, _redirectUrl)}
——————————
推文发布时间：{pubTimeStr}
推文链接：{PluginHelper.AddRedirectToUrls(tweet.Url, _redirectUrl)}

====================
【原推文】来自 {tweet.Origin.Author}

{PluginHelper.AddRedirectToUrls(tweet.Origin.Text, _redirectUrl)}
——————————
原推文发布时间：{originPubTimeStr}
原推文链接：{PluginHelper.AddRedirectToUrls(tweet.Origin.Url, _redirectUrl)}";
        }
        else if (tweet.Type == TweetTypeEnum.RT)
        {
            string originPubTimeStr = TimeZoneInfo
                .ConvertTimeBySystemTimeZoneId(tweet.Origin.PubTime, "China Standard Time")
                .ToString("yyyy-MM-dd HH:mm:ss zzz");

            title = $"【新转发推文】来自 {tweet.Author}";
            text = $@"【原推文】来自 {tweet.Origin.Author}

{PluginHelper.AddRedirectToUrls(tweet.Origin.Text, _redirectUrl)}
——————————
原推文发布时间：{originPubTimeStr}
原推文链接：{PluginHelper.AddRedirectToUrls(tweet.Origin.Url, _redirectUrl)}";
        }
        else
        {
            throw new NotSupportedException($"Not Supported tweet type {tweet.Type}");
        }

        if (tweet.ImageUrls is not null) text += "\n图片：\n";

        if (tweet.HasVideo || (tweet.Origin?.HasVideo == true))
        {
            string videoUrl = await _cosSvc.UploadVideoAsync(tweet.Url, tweet.PubTime, cancellationToken);
            text += $"\n\n视频：{videoUrl}\n";
        }

        return (title, text);
    }

    private async Task<List<string>> GetTweetImgUrlsAsync(Tweet tweet, CancellationToken cancellationToken = default)
    {
        List<string> result = new();

        if (tweet.ImageUrls is not null)
        {
            foreach (string url in tweet.ImageUrls)
            {
                try
                {
                    result.Add(await _cosSvc.UploadImageAsync(url, cancellationToken));
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "");
                    continue;
                }
            }
        }
        else if (tweet.Origin?.ImageUrls is not null)
        {
            foreach (string url in tweet.Origin.ImageUrls)
            {
                try
                {
                    result.Add(await _cosSvc.UploadImageAsync(url, cancellationToken));
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "");
                    continue;
                }
            }
        }

        return result;
    }

    private async Task<List<Paragraph>> TweetToRichTextAsync(
        Tweet tweet, string text, CancellationToken cancellationToken = default)
    {
        List<Paragraph> richText = QQBotService.TextToParagraphs(text);

        if (tweet.ImageUrls is not null)
        {
            richText.AddRange(
                await QQBotService.ImgagesToParagraphsAsync(tweet.ImageUrls, _cosSvc, cancellationToken));
        }
        else if (tweet.Origin?.ImageUrls is not null)
        {
            richText.AddRange(
                await QQBotService.ImgagesToParagraphsAsync(tweet.Origin.ImageUrls, _cosSvc, cancellationToken));
        }

        if (tweet.HasVideo)
        {
            richText.AddRange(
                await QQBotService.VideoToParagraphsAsync(tweet.Url, tweet.PubTime, _cosSvc, cancellationToken));
        }
        else if (tweet.Origin?.HasVideo is true)
        {
            richText.AddRange(
                await QQBotService.VideoToParagraphsAsync(tweet.Origin.Url, tweet.Origin.PubTime, _cosSvc, cancellationToken));
        }

        return richText;
    }
}
