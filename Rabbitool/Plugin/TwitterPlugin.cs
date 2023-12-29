using Coravel;
using Coravel.Invocable;
using MyBot.Models.Forum;
using MyBot.Models.MessageModels;
using Newtonsoft.Json;
using Rabbitool.Common.Configs;
using Rabbitool.Common.Util;
using Rabbitool.Model.DTO.QQBot;
using Rabbitool.Model.DTO.Twitter;
using Rabbitool.Model.Entity.Subscribe;
using Rabbitool.Repository.Subscribe;
using Rabbitool.Service;
using Serilog;

namespace Rabbitool.Plugin;

public class TwitterPlugin : BasePlugin, IPlugin, ICancellableInvocable
{
    private readonly TwitterSubscribeConfigRepository _configRepo;
    private readonly TwitterSubscribeRepository _repo;

    private readonly Dictionary<string, Dictionary<DateTime, Tweet>> _storedTweets = new();
    private readonly TwitterService _svc;

    public TwitterPlugin(QQBotService qbSvc, CosService cosSvc) : base(qbSvc, cosSvc)
    {
        _svc = new TwitterService();

        SubscribeDbContext dbCtx = new(Settings.R.DbPath);
        _repo = new TwitterSubscribeRepository(dbCtx);
        _configRepo = new TwitterSubscribeConfigRepository(dbCtx);
    }

    public CancellationToken CancellationToken { get; set; }

    public async Task InitAsync(IServiceProvider services, CancellationToken ct = default)
    {
        services.UseScheduler(scheduler =>
                scheduler
                    .ScheduleAsync(async () => await CheckAllAsync(ct))
                    .EverySeconds(5)
                    .PreventOverlapping("TwitterPlugin"))
            .OnError(ex => Log.Error(ex, "[Twitter] {msg}", ex.Message));
    }

    public async Task CheckAllAsync(CancellationToken ct = default)
    {
        if (CancellationToken.IsCancellationRequested)
            return;

        List<TwitterSubscribeEntity> records = await _repo.GetAllAsync(true, ct);
        if (records.Count == 0)
        {
            Log.Verbose("[Twitter] There isn't any twitter subscribe yet!");
            return;
        }

        List<Task> tasks = new();
        foreach (TwitterSubscribeEntity record in records)
            tasks.Add(CheckAsync(record, ct));
        await Task.WhenAll(tasks);
    }

    private async Task CheckAsync(TwitterSubscribeEntity record, CancellationToken ct = default)
    {
        try
        {
            Tweet tweet = await _svc.GetLatestTweetAsync(record.ScreenName, ct);
            if (tweet.PubTime <= record.LastTweetTime)
            {
                Log.Debug("[Twitter] No new tweet from the twitter user {name}(screenName: {screenName}).",
                    tweet.Author, tweet.AuthorScreenName);
                return;
            }

            async Task FnAsync(Tweet tweet)
            {
                await PushTweetAsync(tweet, record, ct);

                record.LastTweetTime = tweet.PubTime;
                record.LastTweetId = tweet.Id;
                await _repo.SaveAsync(ct);
                Log.Debug("[Twitter] Succeeded to updated the twitter user {name}(screenName: {screenName})'s record.",
                    tweet.Author, tweet.AuthorScreenName);
            }

            DateTime now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeUtil.CST);
            if (now.Hour is >= 0 and <= 5)
            {
                if (!_storedTweets.ContainsKey(tweet.AuthorScreenName))
                    _storedTweets[tweet.AuthorScreenName] = new Dictionary<DateTime, Tweet>();
                if (!_storedTweets[tweet.AuthorScreenName].ContainsKey(tweet.PubTime))
                    _storedTweets[tweet.AuthorScreenName][tweet.PubTime] = tweet;

                Log.Debug(
                    "[Twitter] Tweet message of the user {name}(screenName: {screenName}) is skipped because it's curfew time now.",
                    tweet.Author, tweet.AuthorScreenName);
                return;
            }

            if (_storedTweets.TryGetValue(tweet.AuthorScreenName, out Dictionary<DateTime, Tweet>? storedTweets)
                && storedTweets.Count != 0)
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
            Log.Error(ex, "[Twitter] Failed to push tweet message!\nName: {name}\nScreenName: {screenName}",
                record.Name, record.ScreenName);
        }
    }

    private async Task PushTweetAsync(Tweet tweet, TwitterSubscribeEntity subscribe, CancellationToken ct = default)
    {
        (string title, string text) = await TweetToStrAsync(tweet, ct);
        RichText richText = await TweetToRichTextAsync(tweet, text, ct);
        List<string>? imgUrls = GetTweetImgUrls(tweet);

        List<TwitterSubscribeConfigEntity> configs = await _configRepo.GetAllAsync(subscribe.ScreenName, ct: ct);
        foreach (QQChannelSubscribeEntity channel in subscribe.QQChannels)
        {
            if (!await QbSvc.ExistChannelAsync(channel.ChannelId))
            {
                Log.Warning("[Twitter] The channel {channelName}(id: {channelId}) doesn't exist!",
                    channel.ChannelName, channel.ChannelId);
                continue;
            }

            TwitterSubscribeConfigEntity config = configs.First(c => c.QQChannel.ChannelId == channel.ChannelId);
            if (tweet.Type == TweetTypeEnum.RT && config.RtPush == false)
                continue;
            if (config.PushToThread)
            {
                await QbSvc.PostThreadAsync(
                    channel.ChannelId, channel.ChannelName, title, JsonConvert.SerializeObject(richText), ct);
                Log.Information(
                    "[Twitter] Succeeded to push the tweet message from the user {name}(screenName: {screenName}).",
                    tweet.Author, tweet.AuthorScreenName);
                continue;
            }

            await QbSvc.PushCommonMsgAsync(
                channel.ChannelId, channel.ChannelName, $"{title}\n\n{text}", imgUrls, ct);
            Log.Information(
                "[Twitter] Succeeded to push the tweet message from the user {name}(screenName: {screenName}).",
                tweet.Author, tweet.AuthorScreenName);
        }
    }

    private async Task<(string title, string text)> TweetToStrAsync(Tweet tweet, CancellationToken ct = default)
    {
        string title;
        string text;

        if (tweet.Origin == null)
        {
            title = $"【新推文】来自 {tweet.Author}";
            text = $"""
                    {tweet.Text.AddRedirectToUrls()}
                    ——————————
                    推文链接：{tweet.Url.AddRedirectToUrls()}
                    """;
        }
        else if (tweet.Type == TweetTypeEnum.Quote)
        {
            title = $"【新带评论转发推文】来自 {tweet.Author}";
            text = $"""
                    {tweet.Text.AddRedirectToUrls()}
                    ——————————
                    推文链接：{tweet.Url.AddRedirectToUrls()}

                    ====================
                    【原推文】来自 {tweet.Origin.Author}

                    {tweet.Origin.Text.AddRedirectToUrls()}
                    """;
        }
        else if (tweet.Type == TweetTypeEnum.RT)
        {
            title = $"【新转发推文】来自 {tweet.Author}";
            text = $"""
                    【原推文】来自 {tweet.Origin.Author}

                    {tweet.Origin.Text.AddRedirectToUrls()}
                    ——————————
                    原推文链接：{tweet.Origin.Url.AddRedirectToUrls()}
                    """;
        }
        else
        {
            throw new NotSupportedException($"Not Supported tweet type {tweet.Type}");
        }

        string? videoUrl = null;
        if (tweet.VideoUrl != null)
            videoUrl = tweet.VideoUrl;
        else if (tweet.Origin?.VideoUrl != null)
            videoUrl = tweet.Origin.VideoUrl;

        if (videoUrl != null)
        {
            videoUrl = await CosSvc.UploadVideoAsync(videoUrl, tweet.PubTime, ct);
            text += $"\n\n视频下载直链：{videoUrl}";
        }

        return (title, text);
    }

    private List<string>? GetTweetImgUrls(Tweet tweet)
    {
        return tweet.ImageUrls ?? tweet.Origin?.ImageUrls;
    }

    private async Task<(MessageMarkdown markdown, List<string>? otherImgs)> TweetToMarkdownAsync(Tweet tweet,
        CancellationToken ct = default)
    {
        List<string> otherImages = new();
        string templateId = Settings.R.QQBot.MarkdownTemplateIds!.TextOnly;
        MarkdownTemplateParams templateParams = new()
        {
            Info = "新推文",
            From = tweet.Author,
            Text = tweet.Text.AddRedirectToUrls(),
            Url = tweet.Url.AddRedirectToUrls()
        };

        if (tweet.ImageUrls != null)
        {
            templateParams.ImageUrl = tweet.ImageUrls[0];
            if (tweet.ImageUrls.Count > 1)
                otherImages.AddRange(tweet.ImageUrls.GetRange(1, tweet.ImageUrls.Count - 1));
            templateId = Settings.R.QQBot.MarkdownTemplateIds.WithImage;
        }

        switch (tweet.Type)
        {
            case TweetTypeEnum.Common:
                break;

            case TweetTypeEnum.Quote:
                templateId = Settings.R.QQBot.MarkdownTemplateIds.ContainsOriginTextOnly;
                templateParams.Info = "新带评论转发推文";
                templateParams.Origin = new MarkdownTemplateParams
                {
                    Info = "原推文",
                    From = tweet.Origin!.Author,
                    Text = tweet.Origin!.Text.AddRedirectToUrls(),
                    Url = tweet.Origin!.Url.AddRedirectToUrls()
                };
                if (tweet.Origin.ImageUrls != null)
                {
                    templateParams.ImageUrl = tweet.Origin.ImageUrls[0];
                    if (tweet.Origin.ImageUrls.Count > 1)
                        otherImages.AddRange(tweet.Origin.ImageUrls.GetRange(1, tweet.Origin.ImageUrls.Count - 1));
                    templateId = Settings.R.QQBot.MarkdownTemplateIds.ContainsOriginWithImage;
                }

                break;

            case TweetTypeEnum.RT:
                templateId = Settings.R.QQBot.MarkdownTemplateIds.ContainsOriginTextOnly;
                templateParams.Info = "新转发推文";
                templateParams.Url = tweet.Url.AddRedirectToUrls();
                templateParams.Origin = new MarkdownTemplateParams
                {
                    Info = "原推文",
                    From = tweet.Origin!.Author,
                    Text = tweet.Origin!.Text.AddRedirectToUrls(),
                    Url = tweet.Origin!.Url.AddRedirectToUrls()
                };
                if (tweet.Origin.ImageUrls != null)
                {
                    templateParams.ImageUrl = tweet.Origin.ImageUrls[0];
                    if (tweet.Origin.ImageUrls.Count > 1)
                        otherImages.AddRange(tweet.Origin.ImageUrls.GetRange(1, tweet.Origin.ImageUrls.Count - 1));
                    templateId = Settings.R.QQBot.MarkdownTemplateIds.ContainsOriginWithImage;
                }

                break;

            default:
                throw new NotSupportedException($"Not Supported tweet type {tweet.Type}");
        }

        string? videoUrl = null;
        string? coverUrl = null;
        if (tweet.VideoUrl != null)
        {
            videoUrl = tweet.VideoUrl;
            coverUrl = tweet.ImageUrls?[0];
        }
        else if (tweet.Origin?.VideoUrl != null)
        {
            videoUrl = tweet.Origin.VideoUrl;
            coverUrl = tweet.Origin.ImageUrls?[0];
        }

        if (videoUrl != null)
        {
            videoUrl = await CosSvc.UploadVideoAsync(videoUrl, tweet.PubTime, ct);
            templateParams.Text += $"\n\n[视频下载直链]({videoUrl})";
        }

        if (coverUrl != null)
        {
            coverUrl = await CosSvc.UploadImageAsync(coverUrl, ct);
            templateParams.ImageUrl = coverUrl;
        }

        return (
            new MessageMarkdown
            {
                CustomTemplateId = templateId,
                Params = templateParams.ToMessageMarkdownParams()
            },
            otherImages
        );
    }

    private async Task<RichText> TweetToRichTextAsync(Tweet tweet, string text, CancellationToken ct = default)
    {
        RichText result = QQBotService.TextToRichText(text);

        if (tweet.ImageUrls != null)
            result.Paragraphs.AddRange(
                await QQBotService.ImagesToParagraphsAsync(tweet.ImageUrls, CosSvc, ct));
        else if (tweet.Origin?.ImageUrls != null)
            result.Paragraphs.AddRange(
                await QQBotService.ImagesToParagraphsAsync(tweet.Origin.ImageUrls, CosSvc, ct));

        if (tweet.VideoUrl != null)
            result.Paragraphs.AddRange(
                await QQBotService.VideoToParagraphsAsync(tweet.VideoUrl, tweet.PubTime, CosSvc, ct));
        else if (tweet.Origin?.VideoUrl != null)
            result.Paragraphs.AddRange(
                await QQBotService.VideoToParagraphsAsync(tweet.Origin.VideoUrl, tweet.Origin.PubTime, CosSvc, ct));

        return result;
    }
}