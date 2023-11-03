using Coravel;
using Coravel.Invocable;
using QQChannelFramework.Models.MessageModels;
using Rabbitool.Common.Util;
using Rabbitool.Conf;
using Rabbitool.Model.DTO.Bilibili;
using Rabbitool.Model.DTO.QQBot;
using Rabbitool.Model.Entity.Subscribe;
using Rabbitool.Repository.Subscribe;
using Rabbitool.Service;
using Serilog;

namespace Rabbitool.Plugin;

public class BilibiliPlugin : BasePlugin, IPlugin, ICancellableInvocable
{
    private readonly BilibiliSubscribeConfigRepository _configRepo;
    private readonly BilibiliSubscribeRepository _repo;

    private readonly Dictionary<uint, Dictionary<DateTime, BaseDynamic>> _storedDynamics = new();
    private readonly BilibiliService _svc;
    private int _waitTime;

    public BilibiliPlugin(QQBotService qbSvc, CosService cosSvc) : base(qbSvc, cosSvc)
    {
        _svc = new BilibiliService();
        SubscribeDbContext dbCtx = new(Configs.R.DbPath);
        _repo = new BilibiliSubscribeRepository(dbCtx);
        _configRepo = new BilibiliSubscribeConfigRepository(dbCtx);
    }

    public CancellationToken CancellationToken { get; set; }

    public async Task InitAsync(IServiceProvider services, CancellationToken ct = default)
    {
        await RefreshCookiesAsync(ct);

        services.UseScheduler(scheduler =>
                scheduler
                    .ScheduleAsync(async () =>
                    {
                        TimeSpan sleepTime = TimeSpan.FromSeconds(Random.Shared.Next(5) + 5);
                        Log.Debug($"[Bilibili] Sleep {sleepTime.TotalSeconds + 5}s...");
                        Thread.Sleep(sleepTime);

                        bool wait = await CheckAllAsync(ct);
                        if (wait)
                        {
                            Log.Warning($"[Bilibili] Touching off anti crawler! Wait {_waitTime} minutes...");
                            Thread.Sleep(TimeSpan.FromMinutes(_waitTime));
                            _waitTime += _waitTime <= 60 ? 2 : 0;
                        }
                        else
                        {
                            _waitTime = 2;
                        }
                    })
                    .EverySeconds(10)
                    .PreventOverlapping("BilibiliPlugin"))
            .OnError(ex => Log.Error(ex, "[Bilibili] {msg}", ex.Message));
    }

    public async Task RefreshCookiesAsync(CancellationToken ct = default)
    {
        await _svc.RefreshCookiesAsync(ct);
    }

    public async Task<bool> CheckAllAsync(CancellationToken ct = default)
    {
        if (CancellationToken.IsCancellationRequested)
            return false;

        List<BilibiliSubscribeEntity> records = await _repo.GetAllAsync(true, ct);
        if (records.Count == 0)
        {
            Log.Verbose("[Bilibili] There isn't any bilibili subscribe yet!");
            return false;
        }

        List<Task<bool>> tasks = new();
        foreach (BilibiliSubscribeEntity record in records)
        {
            tasks.Add(CheckDynamicAsync(record, ct));
            tasks.Add(CheckLiveAsync(record, ct));
        }

        bool[] result = await Task.WhenAll(tasks);
        return result.Any(r => r);
    }

    private async Task<bool> CheckDynamicAsync(BilibiliSubscribeEntity record, CancellationToken ct = default)
    {
        try
        {
            BaseDynamic? dy = await _svc.GetLatestDynamicAsync(record.Uid, ct: ct);
            if (dy == null)
                return false;

            if (dy.DynamicUploadTime <= record.LastDynamicTime)
            {
                Log.Debug("[Bilibili] No new dynamic from the bilibili user {uname}(uid: {uid}).",
                    dy.Uname, dy.Uid);
                return false;
            }

            async Task FnAsync(BaseDynamic dy)
            {
                await PushDynamicMsgAsync(dy, record, ct);

                record.LastDynamicTime = dy.DynamicUploadTime;
                record.LastDynamicType = dy.DynamicType;
                await _repo.SaveAsync(ct);
                Log.Debug("[Bilibili] Succeeded to updated the bilibili user {uname}(uid: {uid})'s record.",
                    dy.Uname, dy.Uid);
            }

            // 宵禁时间发不出去，攒着
            DateTime now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeUtil.CST);
            if (now.Hour is >= 0 and <= 5)
            {
                if (!_storedDynamics.ContainsKey(dy.Uid))
                    _storedDynamics[dy.Uid] = new Dictionary<DateTime, BaseDynamic>();
                if (!_storedDynamics[dy.Uid].ContainsKey(dy.DynamicUploadTime))
                    _storedDynamics[dy.Uid][dy.DynamicUploadTime] = dy;

                Log.Debug(
                    "[Bilibili] Dynamic message of the user {uname}(uid: {uid}) is skipped because it's curfew time now.",
                    dy.Uname, dy.Uid);
                return false;
            }

            // 过了宵禁把攒的先发了
            if (_storedDynamics.TryGetValue(dy.Uid, out Dictionary<DateTime, BaseDynamic>? storedDys)
                && storedDys.Count != 0)
            {
                List<DateTime> uploadTimes = storedDys.Keys.ToList();
                uploadTimes.Sort();
                foreach (DateTime uploadTime in uploadTimes)
                {
                    await FnAsync(storedDys[uploadTime]);
                    _storedDynamics[dy.Uid].Remove(uploadTime);
                }

                return false;
            }

            await FnAsync(dy);
            return false;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (BilibiliApiException bex)
        {
            if (bex.Code is -401 or -509 or -799) return true;

            Log.Error(bex, "[Bilibili] Failed to push bilibili dynamic message!\nUname: {name}\nUid: {uid}",
                record.Uname, record.Uid);
            return false;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[Bilibili] Failed to push bilibili dynamic message!\nUname: {name}\nUid: {uid}",
                record.Uname, record.Uid);
            return false;
        }
    }

    private async Task PushDynamicMsgAsync(
        BaseDynamic dy, BilibiliSubscribeEntity record, CancellationToken ct = default)
    {
        (string title, string text, List<string>? imgUrls) = DynamicToStr(dy);

        List<BilibiliSubscribeConfigEntity> configs = await _configRepo.GetAllAsync(record.Uid, ct: ct);
        foreach (QQChannelSubscribeEntity channel in record.QQChannels)
        {
            if (await QbSvc.ExistChannelAsync(channel.ChannelId) == false)
            {
                Log.Warning("[Bilibili] The channel {channelName}(id: {channelId}) doesn't exist!",
                    channel.ChannelName, channel.ChannelId);
                continue;
            }

            BilibiliSubscribeConfigEntity config = configs.First(
                c => c.QQChannel.ChannelId == channel.ChannelId);
            if (config.DynamicPush == false)
                continue;
            if (dy.DynamicType == DynamicTypeEnum.PureForward && config.PureForwardDynamicPush == false)
                continue;

            await QbSvc.PushCommonMsgAsync(channel.ChannelId, channel.ChannelName, title + "\n\n" + text, imgUrls, ct);
            Log.Information("[Bilibili] Succeeded to push the dynamic message from the user {uname}(uid: {uid}).",
                dy.Uname, dy.Uid);
        }
    }

    private (string title, string text, List<string>? imgUrls) DynamicToStr(BaseDynamic dy)
    {
        string title, text;
        switch (dy)
        {
            case CommonDynamic c:
                (title, text) = CommonDynamicToStr(c);
                return (title, text, c.ImageUrls);

            case VideoDynamic v:
                (title, text) = VideoDynamicToStr(v);
                return (title, text, new List<string> { v.VideoThumbnailUrl });

            case ArticleDynamic a:
                (title, text) = ArticleDynamicToStr(a);
                return (title, text, new List<string> { a.ArticleThumbnailUrl });

            case ForwardDynamic f:
                return ForwardDynamicToStr(f);

            default:
                throw new ArgumentException($"Invalid type {dy.GetType().Name} of the dynamic");
        }
    }

    private (string title, string text) CommonDynamicToStr(CommonDynamic dy)
    {
        string title = "【新动态】来自 " + dy.Uname;
        string text;

        if (dy.Reserve == null)
            text = $"""
                    {dy.Text.AddRedirectToUrls()}
                    ——————————
                    动态链接：{dy.DynamicUrl.AddRedirectToUrls()}
                    """;
        else
            text = $"""
                    {dy.Text.AddRedirectToUrls()}

                    {dy.Reserve.Title}
                    预约时间：{dy.Reserve.StartTime:yyyy-MM-dd HH:mm:ss zzz}
                    ——————————
                    动态链接：{dy.DynamicUrl.AddRedirectToUrls()}
                    """;

        return (title, text);
    }

    private (string title, string text) VideoDynamicToStr(VideoDynamic dy)
    {
        string title = "【新b站视频】来自 " + dy.Uname;
        string text;
        if (dy.DynamicText == "")
            text = $"""
                    {dy.VideoTitle}
                    ——————————
                    视频链接：{dy.VideoUrl.AddRedirectToUrls()}
                    """;
        else
            text = $"""
                    {dy.VideoTitle}

                    {dy.DynamicText}
                    ——————————
                    视频链接：{dy.VideoUrl.AddRedirectToUrls()}
                    """;

        return (title, text);
    }

    private (string title, string text) ArticleDynamicToStr(ArticleDynamic dy)
    {
        string title = "【新专栏】来自 " + dy.Uname;
        string text = $"""
                       {dy.ArticleTitle}
                       ——————————
                       专栏链接：{dy.ArticleUrl.AddRedirectToUrls()}
                       """;

        return (title, text);
    }

    private (string title, string text, List<string>? imgUrls) ForwardDynamicToStr(ForwardDynamic dy)
    {
        string title = "【新转发动态】来自 " + dy.Uname;
        string text;
        List<string>? imgUrls = null;

        switch (dy.Origin)
        {
            case string:
                text = $"""
                        {dy.DynamicText.AddRedirectToUrls()}
                        ——————————
                        动态链接：{dy.DynamicUrl.AddRedirectToUrls()}
                        ====================
                        （原动态已被删除）
                        """;
                break;

            case CommonDynamic cOrigin:
                if (cOrigin.Reserve == null)
                    text = $"""
                            {dy.DynamicText.AddRedirectToUrls()}
                            ——————————
                            动态链接：{dy.DynamicUrl.AddRedirectToUrls()}
                            ====================
                            【原动态】来自 {cOrigin.Uname}

                            {cOrigin.Text.AddRedirectToUrls()}
                            """;
                else
                    text = $"""
                            {dy.DynamicText.AddRedirectToUrls()}
                            ——————————
                            动态链接：{dy.DynamicUrl.AddRedirectToUrls()}
                            ====================
                            【原动态】来自 {cOrigin.Uname}

                            {cOrigin.Text.AddRedirectToUrls()}

                            {cOrigin.Reserve.Title}
                            预约时间：{TimeZoneInfo.ConvertTimeFromUtc(cOrigin.Reserve.StartTime, TimeUtil.CST):yyyy-MM-dd HH:mm:ss zzz}
                            """;
                if (cOrigin.ImageUrls?.Count is int and not 0)
                    imgUrls = cOrigin.ImageUrls;
                break;

            case VideoDynamic vOrigin:
                text = $"""
                        {dy.DynamicText.AddRedirectToUrls()}
                        ——————————
                        动态链接：{dy.DynamicUrl.AddRedirectToUrls()}
                        ====================
                        【视频】来自 {vOrigin.Uname}

                        {vOrigin.VideoTitle}

                        视频链接：{vOrigin.VideoUrl.AddRedirectToUrls()}
                        """;
                imgUrls = new List<string> { vOrigin.VideoThumbnailUrl };
                break;

            case ArticleDynamic aOrigin:
                text = $"""
                        ====================
                        【专栏】来自 {aOrigin.Uname}

                        {aOrigin.ArticleTitle}

                        专栏链接：{aOrigin.ArticleUrl.AddRedirectToUrls()}
                        """;
                imgUrls = new List<string> { aOrigin.ArticleThumbnailUrl };
                break;

            case LiveCardDynamic lOrigin:
                text = $"""
                        {dy.DynamicText.AddRedirectToUrls()}
                        动态链接：{dy.DynamicUrl.AddRedirectToUrls()}
                        ====================
                        【直播】来自 {lOrigin.Uname}

                        标题：{lOrigin.Title}
                        开始时间：{lOrigin.LiveStartTime}
                        链接：{$"https://live.bilibili.com/{lOrigin.RoomId}".AddRedirectToUrls()}
                        """;
                break;

            default:
                Log.Error("[Bilibili] The type {type} of origin dynamic is invalid!", dy.Origin.GetType().Name);
                text = "错误：内部错误！";
                break;
        }

        return (title, text, imgUrls);
    }

    private async Task<(MessageMarkdown markdown, List<string>? otherImgs)> DynamicToMarkdownAsync(BaseDynamic dy)
    {
        return dy switch
        {
            CommonDynamic c => await CommonDynamicToMarkdownAsync(c),
            VideoDynamic v => await VideoDynamicToMarkdownAsync(v),
            ArticleDynamic a => await ArticleDynamicToMarkdownAsync(a),
            ForwardDynamic f => await ForwardDynamicToMarkdownAsync(f),
            _ => throw new NotImplementedException()
        };
    }

    private async Task<(MessageMarkdown, List<string>?)> CommonDynamicToMarkdownAsync(CommonDynamic dy)
    {
        MarkdownTemplateParams templateParams = new()
        {
            Info = "新动态",
            From = dy.Uname,
            Url = dy.DynamicUrl.AddRedirectToUrls(),
            ImageUrl = dy.ImageUrls != null && dy.ImageUrls.Count > 0
                ? await CosSvc.UploadImageAsync(dy.ImageUrls[0])
                : null,
            Text = dy.Text.AddRedirectToUrls()
        };

        if (dy.Reserve != null)
            templateParams.Text = $"""
                                   {dy.Text.AddRedirectToUrls()}

                                   {dy.Reserve.Title}
                                   预约时间：{dy.Reserve.StartTime:yyyy-MM-dd HH:mm:ss zzz}
                                   """;

        List<string> otherImgs = new();
        if (dy.ImageUrls?.Count > 1)
        {
            List<Task<string>> tasks = new();
            foreach (string url in dy.ImageUrls)
                tasks.Add(CosSvc.UploadImageAsync(url));
            string[] urls = await Task.WhenAll(tasks);
            otherImgs = urls.ToList();
        }

        return (
            new MessageMarkdown
            {
                CustomTemplateId = dy.ImageUrls != null && dy.ImageUrls.Count != 0
                    ? Configs.R.QQBot.MarkdownTemplateIds!.WithImage
                    : Configs.R.QQBot.MarkdownTemplateIds!.TextOnly,
                Params = templateParams.ToMessageMarkdownParams()
            },
            otherImgs
        );
    }

    private async Task<(MessageMarkdown, List<string>?)> VideoDynamicToMarkdownAsync(VideoDynamic dy)
    {
        MarkdownTemplateParams templateParams = new()
        {
            Info = "新b站视频",
            From = dy.Uname,
            Url = dy.VideoUrl.AddRedirectToUrls(),
            ImageUrl = await CosSvc.UploadImageAsync(dy.VideoThumbnailUrl),
            Text = dy.DynamicText == ""
                ? dy.VideoTitle
                : dy.VideoTitle + "\n" + dy.DynamicText
        };
        return (
            new MessageMarkdown
            {
                CustomTemplateId = Configs.R.QQBot.MarkdownTemplateIds!.WithImage,
                Params = templateParams.ToMessageMarkdownParams()
            },
            null
        );
    }

    private async Task<(MessageMarkdown, List<string>?)> ArticleDynamicToMarkdownAsync(ArticleDynamic dy)
    {
        MarkdownTemplateParams templateParams = new()
        {
            Info = "新专栏",
            From = dy.Uname,
            Url = dy.ArticleUrl.AddRedirectToUrls(),
            ImageUrl = await CosSvc.UploadImageAsync(dy.ArticleThumbnailUrl),
            Text = dy.ArticleTitle
        };
        return (
            new MessageMarkdown
            {
                CustomTemplateId = Configs.R.QQBot.MarkdownTemplateIds!.WithImage,
                Params = templateParams.ToMessageMarkdownParams()
            },
            null
        );
    }

    private async Task<(MessageMarkdown, List<string>?)> ForwardDynamicToMarkdownAsync(ForwardDynamic dy)
    {
        string templateId = "";
        List<string>? otherImgs = null;

        MarkdownTemplateParams templateParams = new()
        {
            Info = "新转发动态",
            From = dy.Uname,
            Url = dy.DynamicUrl.AddRedirectToUrls(),
            Text = dy.DynamicText
        };

        switch (dy.Origin)
        {
            case string:
                templateId = Configs.R.QQBot.MarkdownTemplateIds!.TextOnly;
                templateParams.Text += "\n（原动态已被删除）";
                break;

            case CommonDynamic cOrigin:
                templateId = cOrigin.ImageUrls != null && cOrigin.ImageUrls.Count > 0
                    ? Configs.R.QQBot.MarkdownTemplateIds!.ContainsOriginWithImage
                    : Configs.R.QQBot.MarkdownTemplateIds!.ContainsOriginTextOnly;
                templateParams.Origin = new MarkdownTemplateParams
                {
                    Info = "动态",
                    From = cOrigin.Uname,
                    Url = cOrigin.DynamicUrl.AddRedirectToUrls(),
                    Text = cOrigin.Text,
                    ImageUrl = cOrigin.ImageUrls != null && cOrigin.ImageUrls.Count > 0
                        ? await CosSvc.UploadImageAsync(cOrigin.ImageUrls[0])
                        : null
                };
                if (cOrigin.ImageUrls?.Count > 1)
                {
                    List<Task<string>> tasks = new();
                    foreach (string url in cOrigin.ImageUrls)
                        tasks.Add(CosSvc.UploadImageAsync(url));
                    string[] urls = await Task.WhenAll(tasks);
                    otherImgs = urls.ToList();
                }

                break;

            case VideoDynamic vOrigin:
                templateId = Configs.R.QQBot.MarkdownTemplateIds!.ContainsOriginWithImage;
                templateParams.Origin = new MarkdownTemplateParams
                {
                    Info = "视频",
                    From = vOrigin.Uname,
                    Url = vOrigin.VideoUrl.AddRedirectToUrls(),
                    Text = vOrigin.DynamicText == ""
                        ? vOrigin.VideoTitle
                        : vOrigin.VideoTitle + "\n" + vOrigin.DynamicText,
                    ImageUrl = await CosSvc.UploadImageAsync(vOrigin.VideoThumbnailUrl)
                };
                break;

            case ArticleDynamic aOrigin:
                templateId = Configs.R.QQBot.MarkdownTemplateIds!.ContainsOriginWithImage;
                templateParams.Origin = new MarkdownTemplateParams
                {
                    Info = "",
                    From = aOrigin.Uname,
                    Url = aOrigin.ArticleUrl.AddRedirectToUrls(),
                    Text = aOrigin.ArticleTitle,
                    ImageUrl = await CosSvc.UploadImageAsync(aOrigin.ArticleThumbnailUrl)
                };
                break;

            case LiveCardDynamic lOrigin:
                templateId = Configs.R.QQBot.MarkdownTemplateIds!.ContainsOriginWithImage;
                templateParams.Origin = new MarkdownTemplateParams
                {
                    Info = "直播",
                    From = lOrigin.Uname,
                    Url = $"https://live.bilibili.com/{lOrigin.RoomId}".AddRedirectToUrls(),
                    Text = $"""
                            标题：{lOrigin.Title}
                            开始时间：{lOrigin.LiveStartTime}
                            """,
                    ImageUrl = await CosSvc.UploadImageAsync(lOrigin.CoverUrl)
                };
                break;
        }

        return (
            new MessageMarkdown
            {
                CustomTemplateId = templateId,
                Params = templateParams.ToMessageMarkdownParams()
            },
            otherImgs
        );
    }

    private async Task<bool> CheckLiveAsync(BilibiliSubscribeEntity record, CancellationToken ct = default)
    {
        try
        {
            DateTime now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeUtil.CST);

            Live? live = await _svc.GetLiveAsync(record.Uid, ct);
            if (live == null)
                return false;

            async Task FnAsync(Live live)
            {
                if (now.Hour is >= 0 and <= 5)
                    // 由于开播通知具有极强的时效性，没法及时发出去的话也就没有意义了，因此直接跳过
                    Log.Debug(
                        "[Bilibili] BLive message of the user {uname}(uid: {uid}) is skipped because it's curfew time now.",
                        record.Uname, record.Uid);
                else
                    await PushLiveMsgAsync(live, record, ct);

                record.LastLiveStatus = live.LiveStatus;
                await _repo.SaveAsync(ct);
                Log.Debug("[Bilibili] Succeeded to updated the bilibili user {uname}(uid: {uid})'s record.",
                    live.Uname, live.Uid);
            }

            // 上次查询未处于直播中
            if (record.LastLiveStatus != LiveStatusEnum.Streaming)
            {
                if (live.LiveStatus == LiveStatusEnum.Streaming)    // 现在处于直播中
                    // 开播
                    await FnAsync(live);
                else
                    // 未开播
                    Log.Debug("[Bilibili] No live now from the bilibili user {uname}(uid: {uid}).", live.Uname,
                        live.Uid);
            }
            // 上次查询处于直播中
            else
            {
                if (live.LiveStatus != LiveStatusEnum.Streaming)    // 现在未处于直播中
                {
                    // 下播
                    record.LastLiveStatus = live.LiveStatus;
                    await _repo.SaveAsync(ct);
                    Log.Debug("[Bilibili] Succeeded to updated the bilibili user {uname}(uid: {uid})'s record.",
                        live.Uname, live.Uid);
                }
                else
                {
                    // 直播中
                    Log.Debug("[Bilibili] The bilibili user {uname}(uid: {uid}) is living.", live.Uname, live.Uid);
                }
                
            }

            return false;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (BilibiliApiException bex)
        {
            if (bex.Code is -401 or -509 or -799) return true;

            Log.Error(bex, "[Bilibili] Failed to push bilibili live message!\nUname: {name}\nUid: {uid}",
                record.Uname, record.Uid);
            return false;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[Bilibili] Failed to push bilibili live message!\nUname: {name}\nUid: {uid}",
                record.Uname, record.Uid);
            return false;
        }
    }

    private async Task PushLiveMsgAsync(Live live, BilibiliSubscribeEntity record, CancellationToken ct = default)
    {
        (string title, string text) = LiveToStr(live);

        List<BilibiliSubscribeConfigEntity> configs = await _configRepo.GetAllAsync(record.Uid, ct: ct);
        foreach (QQChannelSubscribeEntity channel in record.QQChannels)
        {
            if (await QbSvc.ExistChannelAsync(channel.ChannelId) == false)
            {
                Log.Warning("[Bilibili] The channel {channelName}(id: {channelId}) doesn't exist!",
                    channel.ChannelName, channel.ChannelId);
                continue;
            }

            BilibiliSubscribeConfigEntity config = configs.First(c => c.QQChannel.ChannelId == channel.ChannelId);
            if (config.LivePush == false) continue;

            await QbSvc.PushCommonMsgAsync(
                channel.ChannelId, channel.ChannelName, title + "\n\n" + text, live.CoverUrl, ct);
            Log.Information("[Bilibili] Succeeded to push the live message from the user {uname}(uid: {uid}).",
                live.Uname, live.Uid);
        }
    }

    private (string title, string text) LiveToStr(Live live)
    {
        string title = "【b站开播】来自 " + live.Uname;
        string text = $"""
                       标题：{live.Title}
                       开播时间：{TimeZoneInfo.ConvertTimeFromUtc((DateTime)live.LiveStartTime!, TimeUtil.CST):yyyy-MM-dd HH:mm:ss zzz}
                       链接：{("https://live.bilibili.com/" + live.RoomId).AddRedirectToUrls()}
                       """;

        return (title, text);
    }

    private async Task<MessageMarkdown> LiveToMarkdownAsync(Live live)
    {
        MarkdownTemplateParams templateParams = new()
        {
            Info = "b站开播",
            From = live.Uname,
            Url = ("https://live.bilibili.com/" + live.RoomId).AddRedirectToUrls(),
            Text = $"""
                    标题：{live.Title}
                    开播时间：{TimeZoneInfo.ConvertTimeFromUtc((DateTime)live.LiveStartTime!, TimeUtil.CST):yyyy-MM-dd HH:mm:ss zzz}
                    """,
            ImageUrl = await CosSvc.UploadImageAsync(live.CoverUrl!)
        };
        return new MessageMarkdown
        {
            CustomTemplateId = Configs.R.QQBot.MarkdownTemplateIds!.WithImage,
            Params = templateParams.ToMessageMarkdownParams()
        };
    }
}