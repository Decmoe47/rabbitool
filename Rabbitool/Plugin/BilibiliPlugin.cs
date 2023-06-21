﻿using Coravel;
using Flurl.Http;
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

public class BilibiliPlugin : BasePlugin, IPlugin
{
    private readonly BilibiliService _svc;
    private readonly BilibiliSubscribeRepository _repo;
    private readonly BilibiliSubscribeConfigRepository _configRepo;

    private readonly Dictionary<uint, Dictionary<DateTime, BaseDynamic>> _storedDynamics = new();

    public BilibiliPlugin(QQBotService qbSvc, CosService cosSvc) : base(qbSvc, cosSvc)
    {
        _svc = new BilibiliService();
        SubscribeDbContext dbCtx = new(Configs.R.DbPath);
        _repo = new BilibiliSubscribeRepository(dbCtx);
        _configRepo = new BilibiliSubscribeConfigRepository(dbCtx);
    }

    public async Task InitAsync(IServiceProvider services, CancellationToken ct = default)
    {
        await RefreshCookiesAsync(ct);

        services.UseScheduler(scheduler =>
            scheduler
                .ScheduleAsync(async () =>
                {
                    TimeSpan sleepTime = TimeSpan.FromSeconds(Random.Shared.Next(10));
                    Log.Debug($"[BilibiliPlugin] Sleep {sleepTime}...");
                    Thread.Sleep(sleepTime);

                    bool wait = await CheckAllAsync(ct);
                    if (wait)
                    {
                        Log.Debug("[BilibiliPlugin] Wait 5 minutes...");
                        Thread.Sleep(TimeSpan.FromMinutes(5));
                    }
                })
                .EverySeconds(5)
                .PreventOverlapping("BilibiliPlugin"))
                .OnError(ex => Log.Error(ex, "Exception from bilibili plugin: {msg}", ex.Message));
    }

    public async Task RefreshCookiesAsync(CancellationToken ct = default)
    {
        await _svc.RefreshCookiesAsync(ct);
    }

    public async Task<bool> CheckAllAsync(CancellationToken ct = default)
    {
        List<BilibiliSubscribeEntity> records = await _repo.GetAllAsync(true, ct);
        if (records.Count == 0)
        {
            Log.Debug("There isn't any bilibili subscribe yet!");
            return false;
        }

        List<Task> tasks = new();
        foreach (BilibiliSubscribeEntity record in records)
        {
            tasks.Add(CheckDynamicAsync(record, ct));
            tasks.Add(CheckLiveAsync(record, ct));
        }
        try
        {
            await Task.WhenAll(tasks);
        }
        catch (Exception ex)
        {
            if (ex.Message.Contains("-401") || ex.Message.Contains("-509") || ex.Message.Contains("-799"))
            {
                return true;
            }
            else
            {
                await RefreshCookiesAsync(ct);
                throw;
            }
        }
        return false;
    }

    private async Task CheckDynamicAsync(BilibiliSubscribeEntity record, CancellationToken ct = default)
    {
        try
        {
            BaseDynamic? dy = await _svc.GetLatestDynamicAsync(record.Uid, ct: ct);
            if (dy == null)
                return;

            if (dy.DynamicUploadTime <= record.LastDynamicTime)
            {
                Log.Debug("No new dynamic from the bilibili user {uname}(uid: {uid}).", dy.Uname, dy.Uid);
                return;
            }

            async Task FnAsync(BaseDynamic dy)
            {
                await PushDynamicMsgAsync(dy, record, ct);

                record.LastDynamicTime = dy.DynamicUploadTime;
                record.LastDynamicType = dy.DynamicType;
                await _repo.SaveAsync(ct);
                Log.Debug("Succeeded to updated the bilibili user {uname}(uid: {uid})'s record.",
                    dy.Uname, dy.Uid);
            }

            // 宵禁时间发不出去，攒着
            DateTime now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeUtil.CST);
            if (now.Hour >= 0 && now.Hour <= 5)
            {
                if (!_storedDynamics.ContainsKey(dy.Uid))
                    _storedDynamics[dy.Uid] = new Dictionary<DateTime, BaseDynamic>();
                if (!_storedDynamics[dy.Uid].ContainsKey(dy.DynamicUploadTime))
                    _storedDynamics[dy.Uid][dy.DynamicUploadTime] = dy;

                Log.Debug("Dynamic message of the user {uname}(uid: {uid}) is skipped because it's curfew time now.",
                    dy.Uname, dy.Uid);
                return;
            }

            // 过了宵禁把攒的先发了
            if (_storedDynamics.TryGetValue(dy.Uid, out Dictionary<DateTime, BaseDynamic>? storedDys)
                && storedDys != null
                && storedDys.Count != 0)
            {
                List<DateTime> uploadTimes = storedDys.Keys.ToList();
                uploadTimes.Sort();
                foreach (DateTime uploadTime in uploadTimes)
                {
                    await FnAsync(storedDys[uploadTime]);
                    _storedDynamics[dy.Uid].Remove(uploadTime);
                }
                return;
            }

            await FnAsync(dy);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to push dynamic message!\nUid: {uid}\nUname: {uname}",
                record.Uid, record.Uname);
        }
    }

    private async Task PushDynamicMsgAsync(
        BaseDynamic dy, BilibiliSubscribeEntity record, CancellationToken ct = default)
    {
        (string title, string text, List<string>? imgUrls) = DynamicToStr(dy);

        List<Task> tasks = new();
        List<BilibiliSubscribeConfigEntity> configs = await _configRepo.GetAllAsync(record.Uid, ct: ct);
        foreach (QQChannelSubscribeEntity channel in record.QQChannels)
        {
            if (await _qbSvc.ExistChannelAsync(channel.ChannelId) == false)
            {
                Log.Warning("The channel {channelName}(id: {channelId}) doesn't exist!",
                    channel.ChannelName, channel.ChannelId);
                continue;
            }

            BilibiliSubscribeConfigEntity config = configs.First(c => c.QQChannel.ChannelId == channel.ChannelId);
            if (config.DynamicPush == false)
                continue;
            if (dy.DynamicType == DynamicTypeEnum.PureForward && config.PureForwardDynamicPush == false)
                continue;

            tasks.Add(_qbSvc.PushCommonMsgAsync(
                channel.ChannelId, channel.ChannelName, title + "\n\n" + text, imgUrls, ct));
            Log.Information("Succeeded to push the dynamic message from the user {uname}(uid: {uid}).",
                dy.Uname, dy.Uid);
        }

        await Task.WhenAll(tasks);
        return;
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
                return (title, text, new List<string>() { v.VideoThumbnailUrl });

            case ArticleDynamic a:
                (title, text) = ArticleDynamicToStr(a);
                return (title, text, new List<string>() { a.ArticleThumbnailUrl });

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
        {
            text = $"""
                {dy.Text.AddRedirectToUrls()}
                ——————————
                动态链接：{dy.DynamicUrl.AddRedirectToUrls()}
                """;
        }
        else
        {
            text = $"""
                {dy.Text.AddRedirectToUrls()}

                {dy.Reserve.Title}
                预约时间：{dy.Reserve.StartTime:yyyy-MM-dd HH:mm:ss zzz}
                ——————————
                动态链接：{dy.DynamicUrl.AddRedirectToUrls()}
                """;
        }

        return (title, text);
    }

    private (string title, string text) VideoDynamicToStr(VideoDynamic dy)
    {
        string title = "【新b站视频】来自 " + dy.Uname;
        string text;
        if (dy.DynamicText == "")
        {
            text = $"""
                {dy.VideoTitle}
                ——————————
                视频链接：{dy.VideoUrl.AddRedirectToUrls()}
                """;
        }
        else
        {
            text = $"""
                {dy.VideoTitle}

                {dy.DynamicText}
                ——————————
                视频链接：{dy.VideoUrl.AddRedirectToUrls()}
                """;
        }

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
                {
                    text = $"""
                        {dy.DynamicText.AddRedirectToUrls()}
                        ——————————
                        动态链接：{dy.DynamicUrl.AddRedirectToUrls()}
                        ====================
                        【原动态】来自 {cOrigin.Uname}

                        {cOrigin.Text.AddRedirectToUrls()}
                        """;
                }
                else
                {
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
                }
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
                imgUrls = new List<string>() { vOrigin.VideoThumbnailUrl };
                break;

            case ArticleDynamic aOrigin:
                text = $"""
                    ====================
                    【专栏】来自 {aOrigin.Uname}

                    {aOrigin.ArticleTitle}

                    专栏链接：{aOrigin.ArticleUrl.AddRedirectToUrls()}
                    """;
                imgUrls = new List<string>() { aOrigin.ArticleThumbnailUrl };
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
                Log.Error("The type {type} of origin dynamic is invalid!", dy.Origin.GetType().Name);
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
            _ => throw new NotImplementedException(),
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
                ? await _cosSvc.UploadImageAsync(dy.ImageUrls[0])
                : null,
            Text = dy.Text.AddRedirectToUrls()
        };

        if (dy.Reserve != null)
        {
            templateParams.Text = $"""
                {dy.Text.AddRedirectToUrls()}

                {dy.Reserve.Title}
                预约时间：{dy.Reserve.StartTime:yyyy-MM-dd HH:mm:ss zzz}
                """;
        }

        List<string> otherImgs = new();
        if (dy.ImageUrls?.Count > 1)
        {
            List<Task<string>> tasks = new();
            foreach (string url in dy.ImageUrls)
                tasks.Add(_cosSvc.UploadImageAsync(url));
            string[] urls = await Task.WhenAll(tasks);
            otherImgs = urls.ToList();
        }

        return (
            new MessageMarkdown()
            {
                CustomTemplateId = dy.ImageUrls != null && dy.ImageUrls.Count != 0
                    ? Configs.R.MarkdownTemplateIds!.WithImage
                    : Configs.R.MarkdownTemplateIds!.TextOnly,
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
            ImageUrl = await _cosSvc.UploadImageAsync(dy.VideoThumbnailUrl),
            Text = dy.DynamicText == ""
                ? dy.VideoTitle
                : dy.VideoTitle + "\n" + dy.DynamicText
        };
        return (
            new MessageMarkdown()
            {
                CustomTemplateId = Configs.R.MarkdownTemplateIds!.WithImage,
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
            ImageUrl = await _cosSvc.UploadImageAsync(dy.ArticleThumbnailUrl),
            Text = dy.ArticleTitle
        };
        return (
            new MessageMarkdown()
            {
                CustomTemplateId = Configs.R.MarkdownTemplateIds!.WithImage,
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
                templateId = Configs.R.MarkdownTemplateIds!.ContainsOriginDeleted;
                break;

            case CommonDynamic cOrigin:
                templateId = cOrigin.ImageUrls != null && cOrigin.ImageUrls.Count > 0
                    ? Configs.R.MarkdownTemplateIds!.ContainsOriginTextOnly
                    : Configs.R.MarkdownTemplateIds!.ContainsOriginWithImage;
                templateParams.Origin = new()
                {
                    Info = "动态",
                    From = cOrigin.Uname,
                    Url = cOrigin.Text.AddRedirectToUrls(),
                    Text = cOrigin.Text,
                    ImageUrl = cOrigin.ImageUrls != null && cOrigin.ImageUrls.Count > 0
                        ? await _cosSvc.UploadImageAsync(cOrigin.ImageUrls[0])
                        : null,
                };
                if (cOrigin.ImageUrls?.Count > 1)
                {
                    List<Task<string>> tasks = new();
                    foreach (string url in cOrigin.ImageUrls)
                        tasks.Add(_cosSvc.UploadImageAsync(url));
                    string[] urls = await Task.WhenAll(tasks);
                    otherImgs = urls.ToList();
                }
                break;

            case VideoDynamic vOrigin:
                templateId = Configs.R.MarkdownTemplateIds!.ContainsOriginWithImage;
                templateParams.Origin = new()
                {
                    Info = "视频",
                    From = vOrigin.Uname,
                    Url = vOrigin.VideoUrl.AddRedirectToUrls(),
                    Text = vOrigin.DynamicText == ""
                        ? vOrigin.VideoTitle
                        : vOrigin.VideoTitle + "\n" + vOrigin.DynamicText,
                    ImageUrl = await _cosSvc.UploadImageAsync(vOrigin.VideoThumbnailUrl)
                };
                break;

            case ArticleDynamic aOrigin:
                templateId = Configs.R.MarkdownTemplateIds!.ContainsOriginWithImage;
                templateParams.Origin = new()
                {
                    Info = "",
                    From = aOrigin.Uname,
                    Url = aOrigin.ArticleUrl.AddRedirectToUrls(),
                    Text = aOrigin.ArticleTitle,
                    ImageUrl = await _cosSvc.UploadImageAsync(aOrigin.ArticleThumbnailUrl)
                };
                break;

            case LiveCardDynamic lOrigin:
                templateId = Configs.R.MarkdownTemplateIds!.ContainsOriginWithImage;
                templateParams.Origin = new()
                {
                    Info = "直播",
                    From = lOrigin.Uname,
                    Url = $"https://live.bilibili.com/{lOrigin.RoomId}".AddRedirectToUrls(),
                    Text = $"""
                        标题：{lOrigin.Title}
                        开始时间：{lOrigin.LiveStartTime}
                        """,
                    ImageUrl = await _cosSvc.UploadImageAsync(lOrigin.CoverUrl)
                };
                break;
        }
        return (
            new MessageMarkdown()
            {
                CustomTemplateId = templateId,
                Params = templateParams.ToMessageMarkdownParams(),
            },
            otherImgs
        );
    }

    private async Task CheckLiveAsync(BilibiliSubscribeEntity record, CancellationToken ct = default)
    {
        try
        {
            DateTime now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeUtil.CST);

            Live? live = await _svc.GetLiveAsync(record.Uid, ct: ct);
            if (live == null)
                return;

            async Task FnAsync(Live live)
            {
                if (now.Hour >= 0 && now.Hour <= 5)
                {
                    // 由于开播通知具有极强的时效性，没法及时发出去的话也就没有意义了，因此直接跳过
                    Log.Debug("BLive message of the user {uname}(uid: {uid}) is skipped because it's curfew time now.",
                       record.Uname, record.Uid);
                }
                else
                {
                    await PushLiveMsgAsync(live, record, ct);
                }

                record.LastLiveStatus = live.LiveStatus;
                await _repo.SaveAsync(ct);
                Log.Debug("Succeeded to updated the bilibili user {uname}(uid: {uid})'s record.", live.Uname, live.Uid);
            }

            if (record.LastLiveStatus != LiveStatusEnum.Streaming)
            {
                if (live.LiveStatus != LiveStatusEnum.Streaming)
                    // 未开播
                    Log.Debug("No live now from the bilibili user {uname}(uid: {uid}).", live.Uname, live.Uid);
                else
                    // 开播
                    await FnAsync(live);
            }
            else
            {
                if (live.LiveStatus == LiveStatusEnum.Streaming)
                    // 直播中
                    Log.Debug("The bilibili user {uname}(uid: {uid}) is living.", live.Uname, live.Uid);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to push live message!\nUid: {uid}\nUname: {uname}", record.Uid, record.Uname);
        }
    }

    private async Task PushLiveMsgAsync(
        Live live, BilibiliSubscribeEntity record, CancellationToken ct = default)
    {
        (string title, string text) = LiveToStr(live);

        List<Task> tasks = new();
        List<BilibiliSubscribeConfigEntity> configs = await _configRepo.GetAllAsync(record.Uid, ct: ct);
        foreach (QQChannelSubscribeEntity channel in record.QQChannels)
        {
            if (await _qbSvc.ExistChannelAsync(channel.ChannelId) == false)
            {
                Log.Warning("The channel {channelName}(id: {channelId}) doesn't exist!",
                    channel.ChannelName, channel.ChannelId);
                continue;
            }

            BilibiliSubscribeConfigEntity config = configs.First(c => c.QQChannel.ChannelId == channel.ChannelId);
            if (config.LivePush == false) continue;

            tasks.Add(_qbSvc.PushCommonMsgAsync(
                channel.ChannelId, channel.ChannelName, title + "\n\n" + text, live.CoverUrl, ct));
            Log.Information("Succeeded to push the live message from the user {uname}(uid: {uid}).",
                live.Uname, live.Uid);
        }

        await Task.WhenAll(tasks);
        return;
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
            ImageUrl = await _cosSvc.UploadImageAsync(live.CoverUrl!)
        };
        return new MessageMarkdown()
        {
            CustomTemplateId = Configs.R.MarkdownTemplateIds!.WithImage,
            Params = templateParams.ToMessageMarkdownParams(),
        };
    }
}