using Autofac.Annotation;
using Autofac.Annotation.Condition;
using Coravel.Invocable;
using Coravel.Scheduling.Schedule.Interfaces;
using MyBot.Models.Forum;
using MyBot.Models.MessageModels;
using Newtonsoft.Json;
using Rabbitool.Api;
using Rabbitool.Common.Configs;
using Rabbitool.Common.Provider;
using Rabbitool.Common.Util;
using Rabbitool.Model.DTO.QQBot;
using Rabbitool.Model.DTO.Twitter;
using Rabbitool.Model.Entity.Subscribe;
using Rabbitool.Repository.Subscribe;
using Serilog;

namespace Rabbitool.Plugin;

[ConditionalOnProperty("twitter")]
[Component(AutofacScope = AutofacScope.SingleInstance)]
public class TwitterPlugin(
    QQBotApi qqBotApi,
    CosApi cosApi,
    TwitterApi twitterApi,
    TwitterSubscribeRepository repo,
    TwitterSubscribeConfigRepository configRepo,
    MarkdownTemplateIdsConfig templateIds,
    CommonConfig commonConfig,
    ICancellationTokenProvider ctp) : IScheduledPlugin, ICancellableInvocable
{
    private readonly Dictionary<string, Dictionary<DateTime, Tweet>> _storedTweets = new();
    public CancellationToken CancellationToken { get; set; }
    public string Name => "twitter";

    public Task InitAsync()
    {
        return Task.CompletedTask;
    }

    public Action<IScheduler> GetScheduler()
    {
        return scheduler =>
        {
            scheduler
                .ScheduleAsync(async () => await CheckAllAsync())
                .EverySeconds(5)
                .PreventOverlapping("TwitterPlugin");
        };
    }

    private async Task CheckAllAsync()
    {
        if (CancellationToken.IsCancellationRequested)
            return;

        List<TwitterSubscribeEntity> records = await repo.GetAllAsync(true, ctp.Token);
        if (records.Count == 0)
        {
            Log.Verbose("[Twitter] There isn't any twitter subscribe yet!");
            return;
        }

        List<Task> tasks = [];
        tasks.AddRange(records.Select(CheckAsync));
        await Task.WhenAll(tasks);
    }

    private async Task CheckAsync(TwitterSubscribeEntity record)
    {
        try
        {
            Tweet tweet = await twitterApi.GetLatestTweetAsync(record.ScreenName, ctp.Token);
            if (tweet.PubTime <= record.LastTweetTime)
            {
                Log.Debug("[Twitter] No new tweet from the twitter user {name}(screenName: {screenName}).",
                    tweet.Author, tweet.AuthorScreenName);
                return;
            }

            async Task FnAsync(Tweet tweet)
            {
                await PushTweetAsync(tweet, record);

                record.LastTweetTime = tweet.PubTime;
                record.LastTweetId = tweet.Id;
                await repo.SaveAsync(ctp.Token);
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

    private async Task PushTweetAsync(Tweet tweet, TwitterSubscribeEntity subscribe)
    {
        (string title, string text) = await TweetToStrAsync(tweet);
        RichText richText = await TweetToRichTextAsync(tweet, text);
        List<string>? imgUrls = GetTweetImgUrls(tweet);

        List<TwitterSubscribeConfigEntity> configs = await configRepo.GetAllAsync(subscribe.ScreenName, ct: ctp.Token);
        foreach (QQChannelSubscribeEntity channel in subscribe.QQChannels)
        {
            if (!await qqBotApi.ExistChannelAsync(channel.ChannelId))
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
                await qqBotApi.PostThreadAsync(
                    channel.ChannelId, channel.ChannelName, title, JsonConvert.SerializeObject(richText), ctp.Token);
                Log.Information(
                    "[Twitter] Succeeded to push the tweet message from the user {name}(screenName: {screenName}).",
                    tweet.Author, tweet.AuthorScreenName);
                continue;
            }

            await qqBotApi.PushCommonMsgAsync(
                channel.ChannelId, channel.ChannelName, $"{title}\n\n{text}", imgUrls, ctp.Token);
            Log.Information(
                "[Twitter] Succeeded to push the tweet message from the user {name}(screenName: {screenName}).",
                tweet.Author, tweet.AuthorScreenName);
        }
    }

    private async Task<(string title, string text)> TweetToStrAsync(Tweet tweet)
    {
        string title;
        string text;

        if (tweet.Origin == null)
        {
            title = $"【新推文】来自 {tweet.Author}";
            text = $"""
                    {tweet.Text.AddRedirectToUrls(commonConfig.RedirectUrl)}
                    ——————————
                    推文链接：{tweet.Url.AddRedirectToUrls(commonConfig.RedirectUrl)}
                    """;
        }
        else
        {
            switch (tweet.Type)
            {
                case TweetTypeEnum.Quote:
                    title = $"【新带评论转发推文】来自 {tweet.Author}";
                    text = $"""
                            {tweet.Text.AddRedirectToUrls(commonConfig.RedirectUrl)}
                            ——————————
                            推文链接：{tweet.Url.AddRedirectToUrls(commonConfig.RedirectUrl)}

                            ====================
                            【原推文】来自 {tweet.Origin.Author}

                            {tweet.Origin.Text.AddRedirectToUrls(commonConfig.RedirectUrl)}
                            """;
                    break;
                case TweetTypeEnum.RT:
                    title = $"【新转发推文】来自 {tweet.Author}";
                    text = $"""
                            【原推文】来自 {tweet.Origin.Author}

                            {tweet.Origin.Text.AddRedirectToUrls(commonConfig.RedirectUrl)}
                            ——————————
                            原推文链接：{tweet.Origin.Url.AddRedirectToUrls(commonConfig.RedirectUrl)}
                            """;
                    break;
                default:
                    throw new NotSupportedException($"Not Supported tweet type {tweet.Type}");
            }
        }

        string? videoUrl = null;
        if (tweet.VideoUrl != null)
            videoUrl = tweet.VideoUrl;
        else if (tweet.Origin?.VideoUrl != null)
            videoUrl = tweet.Origin.VideoUrl;

        if (videoUrl != null)
        {
            videoUrl = await cosApi.UploadVideoAsync(videoUrl, tweet.PubTime, ctp.Token);
            text += $"\n\n视频下载直链：{videoUrl}";
        }

        return (title, text);
    }

    private List<string>? GetTweetImgUrls(Tweet tweet)
    {
        return tweet.ImageUrls ?? tweet.Origin?.ImageUrls;
    }

    private async Task<(MessageMarkdown markdown, List<string>? otherImgs)> TweetToMarkdownAsync(Tweet tweet)
    {
        List<string> otherImages = [];
        string templateId = templateIds.TextOnly;
        MarkdownTemplateParams templateParams = new()
        {
            Info = "新推文",
            From = tweet.Author,
            Text = tweet.Text.AddRedirectToUrls(commonConfig.RedirectUrl),
            Url = tweet.Url.AddRedirectToUrls(commonConfig.RedirectUrl)
        };

        if (tweet.ImageUrls != null)
        {
            templateParams.ImageUrl = tweet.ImageUrls[0];
            if (tweet.ImageUrls.Count > 1)
                otherImages.AddRange(tweet.ImageUrls.GetRange(1, tweet.ImageUrls.Count - 1));
            templateId = templateIds.WithImage;
        }

        switch (tweet.Type)
        {
            case TweetTypeEnum.Common:
                break;

            case TweetTypeEnum.Quote:
                templateId = templateIds.ContainsOriginTextOnly;
                templateParams.Info = "新带评论转发推文";
                templateParams.Origin = new MarkdownTemplateParams
                {
                    Info = "原推文",
                    From = tweet.Origin!.Author,
                    Text = tweet.Origin!.Text.AddRedirectToUrls(commonConfig.RedirectUrl),
                    Url = tweet.Origin!.Url.AddRedirectToUrls(commonConfig.RedirectUrl)
                };
                if (tweet.Origin.ImageUrls != null)
                {
                    templateParams.ImageUrl = tweet.Origin.ImageUrls[0];
                    if (tweet.Origin.ImageUrls.Count > 1)
                        otherImages.AddRange(tweet.Origin.ImageUrls.GetRange(1, tweet.Origin.ImageUrls.Count - 1));
                    templateId = templateIds.ContainsOriginWithImage;
                }

                break;

            case TweetTypeEnum.RT:
                templateId = templateIds.ContainsOriginTextOnly;
                templateParams.Info = "新转发推文";
                templateParams.Url = tweet.Url.AddRedirectToUrls(commonConfig.RedirectUrl);
                templateParams.Origin = new MarkdownTemplateParams
                {
                    Info = "原推文",
                    From = tweet.Origin!.Author,
                    Text = tweet.Origin!.Text.AddRedirectToUrls(commonConfig.RedirectUrl),
                    Url = tweet.Origin!.Url.AddRedirectToUrls(commonConfig.RedirectUrl)
                };
                if (tweet.Origin.ImageUrls != null)
                {
                    templateParams.ImageUrl = tweet.Origin.ImageUrls[0];
                    if (tweet.Origin.ImageUrls.Count > 1)
                        otherImages.AddRange(tweet.Origin.ImageUrls.GetRange(1, tweet.Origin.ImageUrls.Count - 1));
                    templateId = templateIds.ContainsOriginWithImage;
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
            videoUrl = await cosApi.UploadVideoAsync(videoUrl, tweet.PubTime, ctp.Token);
            templateParams.Text += $"\n\n[视频下载直链]({videoUrl})";
        }

        if (coverUrl != null)
        {
            coverUrl = await cosApi.UploadImageAsync(coverUrl, ctp.Token);
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

    private async Task<RichText> TweetToRichTextAsync(Tweet tweet, string text)
    {
        RichText result = QQBotApi.TextToRichText(text);

        if (tweet.ImageUrls != null)
            result.Paragraphs.AddRange(
                await QQBotApi.ImagesToParagraphsAsync(tweet.ImageUrls, cosApi, ctp.Token));
        else if (tweet.Origin?.ImageUrls != null)
            result.Paragraphs.AddRange(
                await QQBotApi.ImagesToParagraphsAsync(tweet.Origin.ImageUrls, cosApi, ctp.Token));

        if (tweet.VideoUrl != null)
            result.Paragraphs.AddRange(
                await QQBotApi.VideoToParagraphsAsync(tweet.VideoUrl, tweet.PubTime, cosApi, ctp.Token));
        else if (tweet.Origin?.VideoUrl != null)
            result.Paragraphs.AddRange(
                await QQBotApi.VideoToParagraphsAsync(tweet.Origin.VideoUrl, tweet.Origin.PubTime, cosApi, ctp.Token));

        return result;
    }
}