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

    public BilibiliPlugin(
        QQBotService qbSvc,
        CosService cosSvc,
        string dbPath,
        string redirectUrl,
        string userAgent) : base(qbSvc, cosSvc, dbPath, redirectUrl, userAgent)
    {
        _svc = new BilibiliService(userAgent);
        SubscribeDbContext dbCtx = new SubscribeDbContext(_dbPath);
        _repo = new BilibiliSubscribeRepository(dbCtx);
        _configRepo = new BilibiliSubscribeConfigRepository(dbCtx);
    }

    public async Task CheckAllAsync(CancellationToken cancellationToken = default)
    {
        List<BilibiliSubscribeEntity> records = await _repo.GetAllAsync(true, cancellationToken);
        if (records.Count == 0)
        {
            Log.Warning("There isn't any bilibili subscribe yet!");
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

            if (dy.DynamicUploadTime > record.LastDynamicTime)
            {
                await PushDynamicMsgAsync(dy, record, cancellationToken);
                Log.Information("Succeeded to push the dynamic message from the user {uname}(uid: {uid}).",
                    dy.Uname, dy.Uid);

                record.LastDynamicTime = TimeZoneInfo
                    .ConvertTimeBySystemTimeZoneId(dy.DynamicUploadTime, "China Standard Time");
                record.LastDynamicType = dy.DynamicType;
                await _repo.SaveAsync(cancellationToken);
                Log.Information("Succeeded to updated the bilibili user {uname}(uid: {uid})'s record.",
                    dy.Uname, dy.Uid);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to push dynamic message!\nUid: {uid}\nUname: {uname}",
                record.Uid, record.Uname);
        }
    }

    private async Task PushDynamicMsgAsync(
        BaseDynamicDTO dy, BilibiliSubscribeEntity record, CancellationToken cancellationToken = default)
    {
        (string title, string text, List<string>? imgUrls) = DynamicToStr(dy);
        List<string>? redirectImgUrls = null;
        if (imgUrls is not null)
        {
            redirectImgUrls = new List<string>();
            foreach (string imgUrl in imgUrls)
                redirectImgUrls.Add(await _cosSvc.UploadImageAsync(imgUrl));
        }

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

            BilibiliSubscribeConfigEntity? config = configs.FirstOrDefault(c => c.QQChannel.ChannelId == channel.ChannelId);
            if (config is not null)
            {
                if (config.DynamicPush is false) continue;
                if (dy.DynamicType == DynamicTypeEnum.PureForward
                    && config.PureForwardDynamicPush)
                {
                    continue;
                }

                if (redirectImgUrls is null)
                {
                    tasks.Add(_qbSvc.PushCommonMsgAsync(
                        channel.ChannelId, title + "\n\n" + text));
                }
                else
                {
                    tasks.Add(_qbSvc.PushCommonMsgAsync(
                        channel.ChannelId, title + "\n\n" + text, redirectImgUrls));
                }
            }
        }

        await Task.WhenAll(tasks);
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
        string uploadTimeStr = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(
            dy.DynamicUploadTime, "China Standard Time").ToString("yyyy-MM-dd HH:mm:ss zzz");

        if (dy.Reserve is null)
        {
            text = @$"{PluginHelper.AddRedirectToUrls(dy.Text, _redirectUrl)}
——————————
动态发布时间：{uploadTimeStr}
动态链接：{PluginHelper.AddRedirectToUrls(dy.DynamicUrl, _redirectUrl)}";
        }
        else
        {
            text = $@"{PluginHelper.AddRedirectToUrls(dy.Text, _redirectUrl)}

{dy.Reserve.Title}
预约时间：{dy.Reserve.StartTime:yyyy-MM-dd HH:mm:ss zzz}
——————————
动态发布时间：{uploadTimeStr}
动态链接：{PluginHelper.AddRedirectToUrls(dy.DynamicUrl, _redirectUrl)}";
        }

        if (dy.ImageUrls?.Count is int and not 0)
            text += "\n图片：\n";

        return (title, text);
    }

    private (string title, string text) VideoDynamicToStr(VideoDynamicDTO dy)
    {
        string title = "【新视频】来自 " + dy.Uname;

        string dynamicText = dy.DynamicText is "" ? "（无动态文本）" : dy.DynamicText;
        string text = $@"【视频标题】
{dy.VideoTitle}

【动态内容】
{dynamicText}
——————————
视频发布时间：{TimeZoneInfo.ConvertTimeBySystemTimeZoneId(dy.DynamicUploadTime, "China Standard Time"):yyyy-MM-dd HH:mm:ss zzz}
视频链接：{PluginHelper.AddRedirectToUrls(dy.VideoUrl, _redirectUrl)}
视频封面：";

        return (title, text);
    }

    private (string title, string text) ArticleDynamicToStr(ArticleDynamicDTO dy)
    {
        string title = "【新专栏】来自 " + dy.Uname;
        string text = $@"【专栏标题】
{dy.ArticleTitle}
——————————
专栏发布时间：{TimeZoneInfo.ConvertTimeBySystemTimeZoneId(dy.DynamicUploadTime, "China Standard Time"):yyyy-MM-dd HH:mm:ss zzz}
专栏链接：{PluginHelper.AddRedirectToUrls(dy.ArticleUrl, _redirectUrl)}
专栏封面：";

        return (title, text);
    }

    private (string title, string text, List<string>? imgUrls) ForwardDynamicToStr(ForwardDynamicDTO dy)
    {
        string title = "【新转发动态】来自 " + dy.Uname;
        string text;
        List<string>? imgUrls = null;
        string uploadTimeStr = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(
            dy.DynamicUploadTime, "China Standard Time").ToString("yyyy-MM-dd HH:mm:ss zzz");

        switch (dy.Origin)
        {
            case string:
                text = $@"{PluginHelper.AddRedirectToUrls(dy.DynamicText, _redirectUrl)}
——————————
动态发布时间：{uploadTimeStr}
动态链接：{PluginHelper.AddRedirectToUrls(dy.DynamicUrl, _redirectUrl)}

====================
（原动态已被删除）";
                break;

            case CommonDynamicDTO cOrigin:
                string originUploadTimeStr = TimeZoneInfo
                    .ConvertTimeBySystemTimeZoneId(cOrigin.DynamicUploadTime, "China Standard Time")
                    .ToString("yyyy-MM-dd HH:mm:ss zzz");
                if (cOrigin.Reserve is null)
                {
                    text = $@"{PluginHelper.AddRedirectToUrls(dy.DynamicText, _redirectUrl)}
——————————
动态发布时间：{uploadTimeStr}
动态链接：{PluginHelper.AddRedirectToUrls(dy.DynamicUrl, _redirectUrl)}

====================
【原动态】来自 {cOrigin.Uname}

{PluginHelper.AddRedirectToUrls(cOrigin.Text, _redirectUrl)}
——————————
原动态发布时间：{originUploadTimeStr}
原动态链接：{PluginHelper.AddRedirectToUrls(cOrigin.DynamicUrl, _redirectUrl)}";
                }
                else
                {
                    text = $@"{PluginHelper.AddRedirectToUrls(dy.DynamicText, _redirectUrl)}
——————————
动态发布时间：{uploadTimeStr}
动态链接：{PluginHelper.AddRedirectToUrls(dy.DynamicUrl, _redirectUrl)}

====================
【原动态】来自 {cOrigin.Uname}

{PluginHelper.AddRedirectToUrls(cOrigin.Text, _redirectUrl)}

{cOrigin.Reserve.Title}
预约时间：{TimeZoneInfo.ConvertTimeBySystemTimeZoneId(cOrigin.Reserve.StartTime, "China Standard Time"):yyyy-MM-dd HH:mm:ss zzz}
——————————
原动态发布时间：{originUploadTimeStr}
原动态链接：{PluginHelper.AddRedirectToUrls(cOrigin.DynamicUrl, _redirectUrl)}";
                }
                if (cOrigin.ImageUrls?.Count is int and not 0)
                {
                    text += "\n图片：\n";
                    imgUrls = cOrigin.ImageUrls;
                }
                break;

            case VideoDynamicDTO vOrigin:
                text = $@"{PluginHelper.AddRedirectToUrls(dy.DynamicText, _redirectUrl)}

——————————
动态发布时间：{uploadTimeStr}
动态链接：{PluginHelper.AddRedirectToUrls(dy.DynamicUrl, _redirectUrl)}

====================
【视频】来自 P{vOrigin.Uname}

【视频标题】
{vOrigin.VideoTitle}

【原动态内容】
{PluginHelper.AddRedirectToUrls(vOrigin.DynamicText, _redirectUrl)}
——————————
视频发布时间：{TimeZoneInfo.ConvertTimeBySystemTimeZoneId(vOrigin.DynamicUploadTime, "China Standard Time"):yyyy-MM-dd HH:mm:ss zzz}
视频链接：{PluginHelper.AddRedirectToUrls(vOrigin.VideoUrl, _redirectUrl)}
封面：";
                imgUrls = new List<string>() { vOrigin.VideoThumbnailUrl };
                break;

            case ArticleDynamicDTO aOrigin:
                text = $@"动态发布时间：{uploadTimeStr}
动态链接：{PluginHelper.AddRedirectToUrls(dy.DynamicUrl, _redirectUrl)}

====================
【专栏】来自 {aOrigin.Uname}

【专栏标题】
{aOrigin.ArticleTitle}
——————————
专栏发布时间：{TimeZoneInfo.ConvertTimeBySystemTimeZoneId(aOrigin.DynamicUploadTime, "China Standard Time"):yyyy-MM-dd HH:mm:ss zzz}
专栏链接：{PluginHelper.AddRedirectToUrls(aOrigin.ArticleUrl, _redirectUrl)}
封面：";
                imgUrls = new List<string>() { aOrigin.ArticleThumbnailUrl };
                break;

            case LiveCardDynamicDTO lOrigin:
                text = $@"动态发布时间：{uploadTimeStr}
动态链接：{PluginHelper.AddRedirectToUrls(dy.DynamicUrl, _redirectUrl)}

====================
【直播】来自 {lOrigin.Uname}

直播标题：{lOrigin.Title}
直播开始时间：{lOrigin.LiveStartTime}
直播间链接：{PluginHelper.AddRedirectToUrls($"https://live.bilibili.com/{lOrigin.RoomId}", _redirectUrl)}
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
            Live? live = await _svc.GetLiveAsync(record.Uid, cancellationToken: cancellationToken);
            if (live is null)
                return;

            if (record.LastLiveStatus != LiveStatusEnum.Streaming)
            {
                if (live.LiveStatus == LiveStatusEnum.Streaming)
                {
                    await PushLiveMsgAsync(live, record, LiveStatusEnum.Streaming, cancellationToken);
                    Log.Information("Succeeded to push the live message from the user {uname}(uid: {uid}).",
                        live.Uname, live.Uid);

                    record.LastLiveStatus = live.LiveStatus;
                    await _repo.SaveAsync(cancellationToken);
                    Log.Information("Succeeded to updated the bilibili user {uname}(uid: {uid})'s record.",
                        live.Uname, live.Uid);
                }
            }
            else
            {
                if (live.LiveStatus != LiveStatusEnum.Streaming)
                {
                    await PushLiveMsgAsync(live, record, LiveStatusEnum.NoLiveStream, cancellationToken);
                    Log.Information("Succeeded to push the live message from the user {uname}(uid: {uid}).",
                        live.Uname, live.Uid);

                    record.LastLiveStatus = live.LiveStatus;
                    await _repo.SaveAsync(cancellationToken);
                    Log.Information("Succeeded to updated the bilibili user {uname}(uid: {uid})'s record.",
                        live.Uname, live.Uid);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to push live message!\nUid: {uid}\nUname: {uname}",
                record.Uid, record.Uname);
        }
    }

    private async Task PushLiveMsgAsync(
        Live live, BilibiliSubscribeEntity record, LiveStatusEnum liveStatus, CancellationToken cancellationToken = default)
    {
        (string title, string text) = LiveToStr(live);
        List<string>? redirectCoverUrl = live.CoverUrl is string and not ""
            ? new List<string>() { live.CoverUrl } : null;

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

            BilibiliSubscribeConfigEntity? config = configs.FirstOrDefault(c => c.QQChannel.ChannelId == channel.ChannelId);
            if (config is not null)
            {
                if (config.LivePush is false) continue;
                if (liveStatus == LiveStatusEnum.NoLiveStream && !config.LiveEndingPush) continue;
            }

            if (redirectCoverUrl is null)
                tasks.Add(_qbSvc.PushCommonMsgAsync(channel.ChannelId, title + "\n\n" + text));
            else
                tasks.Add(_qbSvc.PushCommonMsgAsync(channel.ChannelId, title + "\n\n" + text, redirectCoverUrl));
        }

        await Task.WhenAll(tasks);
    }

    private (string title, string text) LiveToStr(Live live)
    {
        string title;
        string text;
        if (live.LiveStatus == LiveStatusEnum.Streaming
            && live.LiveStartTime is not null)
        {
            DateTime liveStartTime = TimeZoneInfo
                .ConvertTimeBySystemTimeZoneId((DateTime)live.LiveStartTime!, "China Standard Time");

            title = "【开播】来自 " + live.Uname;
            text = $@"直播标题：{live.Title}
开播时间：{liveStartTime:yyyy-MM-dd HH:mm:ss zzz}
直播间链接：{PluginHelper.AddRedirectToUrls("https://live.bilibili.com/" + live.RoomId, _redirectUrl)}";
            if (live.CoverUrl is string and not "")
                text += "\n封面：\n";
        }
        else
        {
            DateTime now = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.Now, "China Standard Time");

            title = "【下播】来自 " + live.Uname;
            text = $@"下播时间：{now:yyyy-MM-dd HH:mm:ss zzz}
直播间链接：{PluginHelper.AddRedirectToUrls("https://live.bilibili.com/" + live.RoomId, _redirectUrl)}";
        }

        return (title, text);
    }
}
