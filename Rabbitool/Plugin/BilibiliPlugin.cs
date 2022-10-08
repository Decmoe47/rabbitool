using Rabbitool.Common.Util;
using Rabbitool.Model.DTO.Bilibili;
using Rabbitool.Model.Entity.Subscribe;
using Rabbitool.Repository.Subscribe;
using Rabbitool.Service;
using Serilog;

namespace Rabbitool.Plugin;

public class BilibiliPlugin : BasePlugin
{
    private readonly BilibiliService _svc;
    private readonly BilibiliSubscribeRepository _repo;
    private readonly BilibiliSubscribeConfigRepository _configRepo;

    private Dictionary<uint, Dictionary<DateTime, BaseDynamicDTO>> _storedDynamics = new();

    public BilibiliPlugin(
        QQBotService qbSvc,
        CosService cosSvc,
        string dbPath,
        string redirectUrl,
        string userAgent) : base(qbSvc, cosSvc, dbPath, redirectUrl, userAgent)
    {
        _svc = new BilibiliService(userAgent);
        SubscribeDbContext dbCtx = new(_dbPath);
        _repo = new BilibiliSubscribeRepository(dbCtx);
        _configRepo = new BilibiliSubscribeConfigRepository(dbCtx);
    }

    public async Task RefreshCookiesAsync(CancellationToken cancellationToken = default)
    {
        await _svc.RefreshCookiesAsync(cancellationToken);
    }

    public async Task CheckAllAsync(CancellationToken cancellationToken = default)
    {
        List<BilibiliSubscribeEntity> records = await _repo.GetAllAsync(true, cancellationToken);
        if (records.Count == 0)
        {
            Log.Debug("There isn't any bilibili subscribe yet!");
            return;
        }

        List<Task> tasks = new();
        foreach (BilibiliSubscribeEntity record in records)
        {
            tasks.Add(CheckDynamicAsync(record, cancellationToken));
            tasks.Add(CheckLiveAsync(record, cancellationToken));
        }
        await Task.WhenAll(tasks);
    }

    private async Task CheckDynamicAsync(BilibiliSubscribeEntity record, CancellationToken cancellationToken = default)
    {
        try
        {
            BaseDynamicDTO? dy = await _svc.GetLatestDynamicAsync(record.Uid, cancellationToken: cancellationToken);
            if (dy is null)
                return;

            if (dy.DynamicUploadTime <= record.LastDynamicTime)
            {
                Log.Debug("No new dynamic from the bilibili user {uname}(uid: {uid}).", dy.Uname, dy.Uid);
                return;
            }

            async Task FnAsync(BaseDynamicDTO dy)
            {
                bool pushed = await PushDynamicMsgAsync(dy, record, cancellationToken);
                if (pushed)
                {
                    Log.Information("Succeeded to push the dynamic message from the user {uname}(uid: {uid}).",
                        dy.Uname, dy.Uid);
                }

                record.LastDynamicTime = dy.DynamicUploadTime;
                record.LastDynamicType = dy.DynamicType;
                await _repo.SaveAsync(cancellationToken);
                Log.Debug("Succeeded to updated the bilibili user {uname}(uid: {uid})'s record.",
                    dy.Uname, dy.Uid);
            }

            // 宵禁时间发不出去，攒着
            DateTime now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeUtil.CST);
            if (now.Hour >= 0 && now.Hour <= 6)
            {
                if (!_storedDynamics.ContainsKey(dy.Uid))
                    _storedDynamics[dy.Uid] = new Dictionary<DateTime, BaseDynamicDTO>();
                if (!_storedDynamics[dy.Uid].ContainsKey(dy.DynamicUploadTime))
                    _storedDynamics[dy.Uid][dy.DynamicUploadTime] = dy;

                Log.Debug("Dynamic message of the user {uname}(uid: {uid}) is skipped because it's curfew time now.",
                    dy.Uname, dy.Uid);
                return;
            }

            // 过了宵禁把攒的先发了
            if (_storedDynamics.TryGetValue(dy.Uid, out Dictionary<DateTime, BaseDynamicDTO>? storedDys) && storedDys != null && storedDys.Count != 0)
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

    private async Task<bool> PushDynamicMsgAsync(
        BaseDynamicDTO dy, BilibiliSubscribeEntity record, CancellationToken cancellationToken = default)
    {
        (string title, string text, List<string>? imgUrls) = DynamicToStr(dy);
        List<string> redirectImgUrls = new();
        if (imgUrls is not null)
        {
            foreach (string imgUrl in imgUrls)
                redirectImgUrls.Add(await _cosSvc.UploadImageAsync(imgUrl));
        }

        bool pushed = false;
        List<Task> tasks = new();
        List<BilibiliSubscribeConfigEntity> configs = await _configRepo.GetAllAsync(
            record.Uid, cancellationToken: cancellationToken);
        foreach (QQChannelSubscribeEntity channel in record.QQChannels)
        {
            if (await _qbSvc.ExistChannelAsync(channel.ChannelId) is false)
            {
                Log.Warning("The channel {channelName}(id: {channelId}) doesn't exist!",
                    channel.ChannelName, channel.ChannelId);
                continue;
            }

            BilibiliSubscribeConfigEntity config = configs.First(c => c.QQChannel.ChannelId == channel.ChannelId);
            if (config.DynamicPush is false) continue;
            if (dy.DynamicType == DynamicTypeEnum.PureForward && config.PureForwardDynamicPush)
                continue;

            tasks.Add(_qbSvc.PushCommonMsgAsync(channel.ChannelId, title + "\n\n" + text, redirectImgUrls, cancellationToken));
            pushed = true;
        }

        await Task.WhenAll(tasks);
        return pushed;
    }

    private (string title, string text, List<string>? imgUrls) DynamicToStr(BaseDynamicDTO dy)
    {
        string title, text;
        switch (dy)
        {
            case CommonDynamicDTO c:
                (title, text) = CommonDynamicToStr(c);
                return (title, text, c.ImageUrls);

            case VideoDynamicDTO v:
                (title, text) = VideoDynamicToStr(v);
                return (title, text, new List<string>() { v.VideoThumbnailUrl });

            case ArticleDynamicDTO a:
                (title, text) = ArticleDynamicToStr(a);
                return (title, text, new List<string>() { a.ArticleThumbnailUrl });

            case ForwardDynamicDTO f:
                return ForwardDynamicToStr(f);

            default:
                throw new ArgumentException($"Invalid type {dy.GetType().Name} of the dynamic");
        }
    }

    private (string title, string text) CommonDynamicToStr(CommonDynamicDTO dy)
    {
        string title = "【新动态】来自 " + dy.Uname;
        string text;
        string uploadTimeStr = TimeZoneInfo.ConvertTimeFromUtc(
            dy.DynamicUploadTime, TimeUtil.CST).ToString("yyyy-MM-dd HH:mm:ss zzz");

        if (dy.Reserve is null)
        {
            text = @$"{dy.Text.AddRedirectToUrls(_redirectUrl)}
——————————
动态发布时间：{uploadTimeStr}
动态链接：{dy.DynamicUrl.AddRedirectToUrls(_redirectUrl)}";
        }
        else
        {
            text = $@"{dy.Text.AddRedirectToUrls(_redirectUrl)}

{dy.Reserve.Title}
预约时间：{dy.Reserve.StartTime:yyyy-MM-dd HH:mm:ss zzz}
——————————
动态发布时间：{uploadTimeStr}
动态链接：{dy.DynamicUrl.AddRedirectToUrls(_redirectUrl)}";
        }

        if (dy.ImageUrls?.Count is int and not 0)
            text += "\n图片：\n";

        return (title, text);
    }

    private (string title, string text) VideoDynamicToStr(VideoDynamicDTO dy)
    {
        string title = "【新b站视频】来自 " + dy.Uname;

        string dynamicText = dy.DynamicText is "" ? "（无动态文本）" : dy.DynamicText;
        string text = $@"【视频标题】
{dy.VideoTitle}

【动态内容】
{dynamicText}
——————————
视频发布时间：{TimeZoneInfo.ConvertTimeFromUtc(dy.DynamicUploadTime, TimeUtil.CST):yyyy-MM-dd HH:mm:ss zzz}
视频链接：{dy.VideoUrl.AddRedirectToUrls(_redirectUrl)}
视频封面：";

        return (title, text);
    }

    private (string title, string text) ArticleDynamicToStr(ArticleDynamicDTO dy)
    {
        string title = "【新专栏】来自 " + dy.Uname;
        string text = $@"【专栏标题】
{dy.ArticleTitle}
——————————
专栏发布时间：{TimeZoneInfo.ConvertTimeFromUtc(dy.DynamicUploadTime, TimeUtil.CST):yyyy-MM-dd HH:mm:ss zzz}
专栏链接：{dy.ArticleUrl.AddRedirectToUrls(_redirectUrl)}
专栏封面：";

        return (title, text);
    }

    private (string title, string text, List<string>? imgUrls) ForwardDynamicToStr(ForwardDynamicDTO dy)
    {
        string title = "【新转发动态】来自 " + dy.Uname;
        string text;
        List<string>? imgUrls = null;
        string uploadTimeStr = TimeZoneInfo.ConvertTimeFromUtc(
            dy.DynamicUploadTime, TimeUtil.CST).ToString("yyyy-MM-dd HH:mm:ss zzz");

        switch (dy.Origin)
        {
            case string:
                text = $@"{dy.DynamicText.AddRedirectToUrls(_redirectUrl)}
——————————
动态发布时间：{uploadTimeStr}
动态链接：{dy.DynamicUrl.AddRedirectToUrls(_redirectUrl)}

====================
（原动态已被删除）";
                break;

            case CommonDynamicDTO cOrigin:
                string originUploadTimeStr = TimeZoneInfo
                    .ConvertTimeFromUtc(cOrigin.DynamicUploadTime, TimeUtil.CST)
                    .ToString("yyyy-MM-dd HH:mm:ss zzz");
                if (cOrigin.Reserve is null)
                {
                    text = $@"{dy.DynamicText.AddRedirectToUrls(_redirectUrl)}
——————————
动态发布时间：{uploadTimeStr}
动态链接：{dy.DynamicUrl.AddRedirectToUrls(_redirectUrl)}

====================
【原动态】来自 {cOrigin.Uname}

{cOrigin.Text.AddRedirectToUrls(_redirectUrl)}
——————————
原动态发布时间：{originUploadTimeStr}
原动态链接：{cOrigin.DynamicUrl.AddRedirectToUrls(_redirectUrl)}";
                }
                else
                {
                    text = $@"{dy.DynamicText.AddRedirectToUrls(_redirectUrl)}
——————————
动态发布时间：{uploadTimeStr}
动态链接：{dy.DynamicUrl.AddRedirectToUrls(_redirectUrl)}

====================
【原动态】来自 {cOrigin.Uname}

{cOrigin.Text.AddRedirectToUrls(_redirectUrl)}

{cOrigin.Reserve.Title}
预约时间：{TimeZoneInfo.ConvertTimeFromUtc(cOrigin.Reserve.StartTime, TimeUtil.CST):yyyy-MM-dd HH:mm:ss zzz}
——————————
原动态发布时间：{originUploadTimeStr}
原动态链接：{cOrigin.DynamicUrl.AddRedirectToUrls(_redirectUrl)}";
                }
                if (cOrigin.ImageUrls?.Count is int and not 0)
                {
                    text += "\n图片：\n";
                    imgUrls = cOrigin.ImageUrls;
                }
                break;

            case VideoDynamicDTO vOrigin:
                text = $@"{dy.DynamicText.AddRedirectToUrls(_redirectUrl)}

——————————
动态发布时间：{uploadTimeStr}
动态链接：{dy.DynamicUrl.AddRedirectToUrls(_redirectUrl)}

====================
【视频】来自 {vOrigin.Uname}

【视频标题】
{vOrigin.VideoTitle}

【原动态内容】
{vOrigin.DynamicText.AddRedirectToUrls(_redirectUrl)}
——————————
视频发布时间：{TimeZoneInfo.ConvertTimeFromUtc(vOrigin.DynamicUploadTime, TimeUtil.CST):yyyy-MM-dd HH:mm:ss zzz}
视频链接：{vOrigin.VideoUrl.AddRedirectToUrls(_redirectUrl)}
封面：";
                imgUrls = new List<string>() { vOrigin.VideoThumbnailUrl };
                break;

            case ArticleDynamicDTO aOrigin:
                text = $@"动态发布时间：{uploadTimeStr}
动态链接：{dy.DynamicUrl.AddRedirectToUrls(_redirectUrl)}

====================
【专栏】来自 {aOrigin.Uname}

【专栏标题】
{aOrigin.ArticleTitle}
——————————
专栏发布时间：{TimeZoneInfo.ConvertTimeFromUtc(aOrigin.DynamicUploadTime, TimeUtil.CST):yyyy-MM-dd HH:mm:ss zzz}
专栏链接：{aOrigin.ArticleUrl.AddRedirectToUrls(_redirectUrl)}
封面：";
                imgUrls = new List<string>() { aOrigin.ArticleThumbnailUrl };
                break;

            case LiveCardDynamicDTO lOrigin:
                text = $@"动态发布时间：{uploadTimeStr}
动态链接：{dy.DynamicUrl.AddRedirectToUrls(_redirectUrl)}

====================
【直播】来自 {lOrigin.Uname}

直播标题：{lOrigin.Title}
直播开始时间：{lOrigin.LiveStartTime}
直播间链接：{$"https://live.bilibili.com/{lOrigin.RoomId}".AddRedirectToUrls(_redirectUrl)}
直播间封面：";
                break;

            default:
                Log.Error("The type {type} of origin dynamic is invalid!", dy.Origin.GetType().Name);
                text = "错误：内部错误！";
                break;
        }

        return (title, text, imgUrls);
    }

    private async Task CheckLiveAsync(BilibiliSubscribeEntity record, CancellationToken cancellationToken = default)
    {
        try
        {
            DateTime now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeUtil.CST);

            Live? live = await _svc.GetLiveAsync(record.Uid, cancellationToken: cancellationToken);
            if (live is null)
                return;

            async Task FnAsync(Live live, LiveStatusEnum liveStatus)
            {
                if (now.Hour >= 0 && now.Hour <= 6)
                {
                    // 由于开播下播通知具有极强的时效性，没法及时发出去的话也就没有意义了，因此直接跳过
                    Log.Debug("BLive message of the user {uname}(uid: {uid}) is skipped because it's curfew time now.",
                       record.Uname, record.Uid);
                }
                else
                {
                    bool pushed = await PushLiveMsgAsync(live, record, liveStatus, cancellationToken);
                    if (pushed)
                    {
                        Log.Information("Succeeded to push the live message from the user {uname}(uid: {uid}).",
                            live.Uname, live.Uid);
                    }
                }

                record.LastLiveStatus = live.LiveStatus;
                await _repo.SaveAsync(cancellationToken);
                Log.Debug("Succeeded to updated the bilibili user {uname}(uid: {uid})'s record.",
                    live.Uname, live.Uid);
            }

            if (record.LastLiveStatus != LiveStatusEnum.Streaming)
            {
                if (live.LiveStatus != LiveStatusEnum.Streaming)
                    // 未开播
                    Log.Debug("No live now from the bilibili user {uname}(uid: {uid}).", live.Uname, live.Uid);
                else
                    // 开播
                    await FnAsync(live, LiveStatusEnum.Streaming);
            }
            else
            {
                if (live.LiveStatus == LiveStatusEnum.Streaming)
                    // 直播中
                    Log.Debug("The bilibili user {uname}(uid: {uid}) is living.", live.Uname, live.Uid);
                else
                    // 下播
                    await FnAsync(live, LiveStatusEnum.NoLiveStream);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to push live message!\nUid: {uid}\nUname: {uname}",
                record.Uid, record.Uname);
        }
    }

    private async Task<bool> PushLiveMsgAsync(
        Live live, BilibiliSubscribeEntity record, LiveStatusEnum liveStatus, CancellationToken cancellationToken = default)
    {
        (string title, string text) = LiveToStr(live);
        List<string>? redirectCoverUrl = live.CoverUrl is string and not ""
            ? new List<string>() { live.CoverUrl } : null;

        bool pushed = false;
        List<Task> tasks = new();
        List<BilibiliSubscribeConfigEntity> configs = await _configRepo.GetAllAsync(
            record.Uid, cancellationToken: cancellationToken);
        foreach (QQChannelSubscribeEntity channel in record.QQChannels)
        {
            if (await _qbSvc.ExistChannelAsync(channel.ChannelId) is false)
            {
                Log.Warning("The channel {channelName}(id: {channelId}) doesn't exist!",
                    channel.ChannelName, channel.ChannelId);
                continue;
            }

            BilibiliSubscribeConfigEntity config = configs.First(c => c.QQChannel.ChannelId == channel.ChannelId);
            if (config.LivePush is false) continue;
            if (liveStatus == LiveStatusEnum.NoLiveStream && !config.LiveEndingPush) continue;

            if (redirectCoverUrl is null)
                tasks.Add(_qbSvc.PushCommonMsgAsync(channel.ChannelId, title + "\n\n" + text, cancellationToken));
            else
                tasks.Add(_qbSvc.PushCommonMsgAsync(channel.ChannelId, title + "\n\n" + text, redirectCoverUrl, cancellationToken));

            pushed = true;
        }

        await Task.WhenAll(tasks);
        return pushed;
    }

    private (string title, string text) LiveToStr(Live live)
    {
        string title;
        string text;
        if (live.LiveStatus == LiveStatusEnum.Streaming
            && live.LiveStartTime is not null)
        {
            DateTime liveStartTime = TimeZoneInfo
                .ConvertTimeFromUtc((DateTime)live.LiveStartTime!, TimeUtil.CST);

            title = "【b站开播】来自 " + live.Uname;
            text = $@"直播标题：{live.Title}
开播时间：{liveStartTime:yyyy-MM-dd HH:mm:ss zzz}
直播间链接：{PluginHelper.AddRedirectToUrls("https://live.bilibili.com/" + live.RoomId, _redirectUrl)}";
            if (live.CoverUrl is string and not "")
                text += "\n封面：\n";
        }
        else
        {
            DateTime now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeUtil.CST);

            title = "【b站下播】来自 " + live.Uname;
            text = $@"下播时间：{now:yyyy-MM-dd HH:mm:ss zzz}
直播间链接：{PluginHelper.AddRedirectToUrls("https://live.bilibili.com/" + live.RoomId, _redirectUrl)}";
        }

        return (title, text);
    }
}
