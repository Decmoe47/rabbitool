using Newtonsoft.Json;
using Rabbitool.Common.Util;
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

    private Dictionary<string, Dictionary<DateTime, Tweet>> _storedTweets = new();

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
            Log.Debug("There isn't any twitter subscribe yet!");
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
            if (tweet.PubTime <= record.LastTweetTime)
            {
                Log.Debug("No new tweet from the twitter user {name}(screenName: {screenName}).",
                    tweet.Author, tweet.AuthorScreenName);
                return;
            }

            async Task FnAsync(Tweet tweet)
            {
                bool pushed = await PushTweetAsync(tweet, record, cancellationToken);
                if (pushed)
                {
                    Log.Information("Succeeded to push the tweet message from the user {name}(screenName: {screenName}).",
                        tweet.Author, tweet.AuthorScreenName);
                }

                record.LastTweetTime = tweet.PubTime;
                record.LastTweetId = tweet.Id;
                await _repo.SaveAsync(cancellationToken);
                Log.Debug("Succeeded to updated the twitter user {name}(screenName: {screenName})'s record.",
                        tweet.Author, tweet.AuthorScreenName);
            }

            DateTime now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeUtil.CST);
            if (now.Hour >= 0 && now.Hour <= 5)
            {
                if (!_storedTweets.ContainsKey(tweet.AuthorScreenName))
                    _storedTweets[tweet.AuthorScreenName] = new Dictionary<DateTime, Tweet>();
                if (!_storedTweets[tweet.AuthorScreenName].ContainsKey(tweet.PubTime))
                    _storedTweets[tweet.AuthorScreenName][tweet.PubTime] = tweet;

                Log.Debug("Tweet message of the user {name}(screenName: {screenName}) is skipped because it's curfew time now.",
                    tweet.Author, tweet.AuthorScreenName);
                return;
            }

            if (_storedTweets.TryGetValue(tweet.AuthorScreenName, out Dictionary<DateTime, Tweet>? storedTweets) && storedTweets != null && storedTweets.Count != 0)
            {
                List<DateTime> pubTimes = storedTweets.Keys.ToList();
                pubTimes.Sort();
                foreach (DateTime pubTime in pubTimes)
                {
                    await FnAsync(storedTweets[pubTime]);
                    _storedTweets[tweet.AuthorScreenName].Remove(pubTime);
                }
                return;
            }

            await FnAsync(tweet);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to push tweet message!\nName: {name}\nScreenName: {screenName}",
                record.Name, record.ScreenName);
        }
    }

    private async Task<bool> PushTweetAsync(
        Tweet tweet, TwitterSubscribeEntity subscribe, CancellationToken cancellationToken = default)
    {
        (string title, string text) = await TweetToStrAsync(tweet, cancellationToken);
        RichText richText = await TweetToRichTextAsync(tweet, text, cancellationToken);
        List<string> imgUrls = await GetTweetImgUrlsAsync(tweet, cancellationToken);

        List<TwitterSubscribeConfigEntity> configs = await _configRepo.GetAllAsync(
            subscribe.ScreenName, cancellationToken: cancellationToken);

        bool pushed = false;
        List<Task> tasks = new();
        foreach (QQChannelSubscribeEntity channel in subscribe.QQChannels)
        {
            if (!await _qbSvc.ExistChannelAsync(channel.ChannelId))
            {
                Log.Warning("The channel {channelName}(id: {channelId}) doesn't exist!",
                    channel.ChannelName, channel.ChannelId);
                continue;
            }

            TwitterSubscribeConfigEntity config = configs.First(c => c.QQChannel.ChannelId == channel.ChannelId);
            if (tweet.Origin is not null)
            {
                if (config.QuotePush is false) continue;
                if (tweet.Type == TweetTypeEnum.RT && !config.RtPush) continue;
            }
            if (config.PushToThread)
            {
                tasks.Add(_qbSvc.PostThreadAsync(channel.ChannelId, title, JsonConvert.SerializeObject(richText), cancellationToken));
                pushed = true;
                continue;
            }

            tasks.Add(_qbSvc.PushCommonMsgAsync(channel.ChannelId, $"{title}\n\n{text}", imgUrls, cancellationToken));
            pushed = true;
        }

        await Task.WhenAll(tasks);
        return pushed;
    }

    private async Task<(string title, string text)> TweetToStrAsync(
        Tweet tweet, CancellationToken cancellationToken = default)
    {
        string title;
        string text;
        string pubTimeStr = TimeZoneInfo
            .ConvertTimeFromUtc(tweet.PubTime, TimeUtil.CST)
            .ToString("yyyy-MM-dd HH:mm:ss zzz");

        if (tweet.Origin is null)
        {
            title = $"【新推文】来自 {tweet.Author}";
            text = $@"{tweet.Text.AddRedirectToUrls(_redirectUrl)}
——————————
推文发布时间：{pubTimeStr}
推文链接：{tweet.Url.AddRedirectToUrls(_redirectUrl)}";
        }
        else if (tweet.Type == TweetTypeEnum.Quote)
        {
            string originPubTimeStr = TimeZoneInfo
                .ConvertTimeFromUtc(tweet.Origin.PubTime, TimeUtil.CST)
                .ToString("yyyy-MM-dd HH:mm:ss zzz");

            title = $"【新带评论转发推文】来自 {tweet.Author}";
            text = $@"{tweet.Text.AddRedirectToUrls(_redirectUrl)}
——————————
推文发布时间：{pubTimeStr}
推文链接：{tweet.Url.AddRedirectToUrls(_redirectUrl)}

====================
【原推文】来自 {tweet.Origin.Author}

{tweet.Origin.Text.AddRedirectToUrls(_redirectUrl)}
——————————
原推文发布时间：{originPubTimeStr}
原推文链接：{tweet.Origin.Url.AddRedirectToUrls(_redirectUrl)}";
        }
        else if (tweet.Type == TweetTypeEnum.RT)
        {
            string originPubTimeStr = TimeZoneInfo
                .ConvertTimeFromUtc(tweet.Origin.PubTime, TimeUtil.CST)
                .ToString("yyyy-MM-dd HH:mm:ss zzz");

            title = $"【新转发推文】来自 {tweet.Author}";
            text = $@"【原推文】来自 {tweet.Origin.Author}

{tweet.Origin.Text.AddRedirectToUrls(_redirectUrl)}
——————————
原推文发布时间：{originPubTimeStr}
原推文链接：{tweet.Origin.Url.AddRedirectToUrls(_redirectUrl)}";
        }
        else
        {
            throw new NotSupportedException($"Not Supported tweet type {tweet.Type}");
        }

        if (tweet.HasVideo || (tweet.Origin?.HasVideo == true))
        {
            string videoUrl = await _cosSvc.UploadVideoAsync(tweet.Url, tweet.PubTime, cancellationToken);
            text += $"\n\n视频下载直链：{videoUrl}";
        }

        if (tweet.ImageUrls?.Count is int and not 0)
            text += "\n图片：";

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

    private async Task<RichText> TweetToRichTextAsync(
        Tweet tweet, string text, CancellationToken cancellationToken = default)
    {
        RichText result = QQBotService.TextToRichText(text);

        if (tweet.ImageUrls is not null)
        {
            result.Paragraphs.AddRange(
                await QQBotService.ImgagesToParagraphsAsync(tweet.ImageUrls, _cosSvc, cancellationToken));
        }
        else if (tweet.Origin?.ImageUrls is not null)
        {
            result.Paragraphs.AddRange(
                await QQBotService.ImgagesToParagraphsAsync(tweet.Origin.ImageUrls, _cosSvc, cancellationToken));
        }

        if (tweet.HasVideo)
        {
            result.Paragraphs.AddRange(
                await QQBotService.VideoToParagraphsAsync(tweet.Url, tweet.PubTime, _cosSvc, cancellationToken));
        }
        else if (tweet.Origin?.HasVideo is true)
        {
            result.Paragraphs.AddRange(
                await QQBotService.VideoToParagraphsAsync(tweet.Origin.Url, tweet.Origin.PubTime, _cosSvc, cancellationToken));
        }

        return result;
    }
}
