using Rabbitool.Common.Util;
using Rabbitool.Model.DTO.Youtube;
using Rabbitool.Model.Entity.Subscribe;
using Rabbitool.Repository.Subscribe;
using Rabbitool.Service;
using Serilog;

namespace Rabbitool.Plugin;

public class YoutubePlugin : BasePlugin
{
    private readonly YoutubeService _svc;
    private readonly YoutubeSubscribeRepository _repo;
    private readonly YoutubeSubscribeConfigRepository _configRepo;

    private readonly Dictionary<string, Dictionary<DateTime, YoutubeVideo>> _storedVideos = new();

    public YoutubePlugin(
        string apiKey,
        QQBotService qbSvc,
        CosService cosSvc,
        string dbPath,
        string redirectUrl,
        string userAgent) : base(qbSvc, cosSvc, dbPath, redirectUrl, userAgent)
    {
        _svc = new YoutubeService(apiKey);

        SubscribeDbContext dbCtx = new(_dbPath);
        _repo = new YoutubeSubscribeRepository(dbCtx);
        _configRepo = new YoutubeSubscribeConfigRepository(dbCtx);
    }

    public async Task CheckAllAsync(CancellationToken ct = default)
    {
        List<YoutubeSubscribeEntity> records = await _repo.GetAllAsync(true, ct);
        if (records.Count == 0)
        {
            Log.Debug("There isn't any youtube subscribe yet!");
            return;
        }

        List<Task> tasks = new();
        foreach (YoutubeSubscribeEntity record in records)
        {
            tasks.Add(CheckAsync(record, ct));
            tasks.Add(CheckUpcomingLiveAsync(record, ct));
        }
        await Task.WhenAll(tasks);
    }

    private async Task CheckAsync(YoutubeSubscribeEntity record, CancellationToken ct = default)
    {
        try
        {
            YoutubeItem item = await _svc.GetLatestTwoVideoOrLiveAsync(
                record.ChannelId, ct: ct);

            DateTime now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeUtil.CST);

            switch (item)
            {
                case YoutubeLive live:
                    if (live.Type == YoutubeTypeEnum.UpcomingLive && !record.AllUpcomingLiveRoomIds.Contains(live.Id))
                    {
                        record.AllUpcomingLiveRoomIds.Add(live.Id);
                        await _repo.SaveAsync(ct);
                        Log.Debug("Succeeded to updated the youtube user({user})'s record.\nChannelId: {channelId}",
                            live.Author, live.ChannelId);

                        await PushUpcomingLiveAsync(live, record, ct);
                    }
                    else if (live.Type == YoutubeTypeEnum.Live && live.Id != record.LastLiveRoomId && !record.AllUpcomingLiveRoomIds.Contains(live.Id))
                    {
                        await PushLiveAndUpdateDatabaseAsync(live, record, ct: ct);
                    }

                    break;

                case YoutubeVideo video:
                    if (video.PubTime <= record.LastVideoPubTime)
                    {
                        Log.Debug("No new youtube video from the youtube user {name}(channelId: {channelId})",
                            video.Author, video.ChannelId);
                        return;
                    }

                    if (now.Hour >= 0 && now.Hour <= 5)
                    {
                        if (!_storedVideos.ContainsKey(video.ChannelId))
                            _storedVideos[video.ChannelId] = new Dictionary<DateTime, YoutubeVideo>();
                        if (!_storedVideos[video.ChannelId].ContainsKey(video.PubTime))
                            _storedVideos[video.ChannelId][video.PubTime] = video;

                        Log.Debug("Youtube video message of the user {name}(channelId: {channelId} is skipped because it's curfew time now.",
                            video.Author, video.ChannelId);
                        return;
                    }

                    if (_storedVideos.TryGetValue(video.ChannelId, out Dictionary<DateTime, YoutubeVideo>? storedVideos)
                        && storedVideos != null && storedVideos.Count != 0)
                    {
                        List<DateTime> pubTimes = storedVideos.Keys.ToList();
                        pubTimes.Sort();
                        foreach (DateTime pubTime in pubTimes)
                        {
                            await PushVideoAndUpdateDatabaseAsync(storedVideos[pubTime], record, ct);
                            _storedVideos[video.ChannelId].Remove(pubTime);
                        }
                        return;
                    }

                    await PushVideoAndUpdateDatabaseAsync(video, record, ct);
                    break;

                default:
                    throw new NotSupportedException($"Not supported type {item.GetType().Name} of the item!");
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to push youtube message!\nName: {name}\nChannelId: {channelId}",
                record.Name, record.ChannelId);
        }
    }

    private async Task CheckUpcomingLiveAsync(YoutubeSubscribeEntity record, CancellationToken ct = default)
    {
        List<string> allUpcomingLiveRoomIdsTmp = record.AllUpcomingLiveRoomIds;
        foreach (string roomId in allUpcomingLiveRoomIdsTmp)
        {
            if (await _svc.IsStreamingAsync(roomId, ct) is YoutubeLive live)
            {
                Log.Debug("Youtube upcoming live (roomId: {roomId}) starts streaming.", roomId);
                await PushLiveAndUpdateDatabaseAsync(live, record, false, ct);
                record.AllUpcomingLiveRoomIds.Remove(roomId);
                await _repo.SaveAsync(ct);
            }
        }
    }

    private async Task PushLiveAndUpdateDatabaseAsync(
        YoutubeLive live, YoutubeSubscribeEntity record, bool saving = true, CancellationToken ct = default)
    {
        DateTime now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeUtil.CST);
        if (now.Hour >= 0 && now.Hour <= 5)
        {
            Log.Debug("Youtube live message of the user {name}(channelId: {channelId} is skipped because it's curfew time now.",
                live.Author, live.ChannelId);
        }
        else
        {
            await PushMsgAsync(live, record, ct);
            Log.Information("Succeeded to push the youtube live message from the user {Author}.\nChannelId: {channelId}",
                live.Author, live.ChannelId);
        }

        record.LastLiveRoomId = live.Id;
        record.LastLiveStartTime = (DateTime)live.ActualStartTime!;
        record.AllArchiveVideoIds.Add(live.Id);

        if (record.AllArchiveVideoIds.Count > 5)
            record.AllArchiveVideoIds.RemoveAt(0);

        if (saving)
            await _repo.SaveAsync(ct);
        Log.Debug("Succeeded to updated the youtube user({user})'s record.\nChannelId: {channelId}",
            live.Author, live.ChannelId);
    }

    private async Task PushUpcomingLiveAsync(
        YoutubeLive live, YoutubeSubscribeEntity record, CancellationToken ct = default)
    {
        DateTime now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeUtil.CST);
        if (now.Hour >= 0 && now.Hour <= 5)
        {
            Log.Debug("Youtube upcoming live message of the user {name}(channelId: {channelId} is skipped because it's curfew time now.",
                live.Author, live.ChannelId);
        }
        else
        {
            bool pushed = await PushMsgAsync(live, record, ct);
            if (pushed)
            {
                Log.Information("Succeeded to push the youtube live message from the user {Author}.\nChannelId: {channelId}",
                    live.Author, live.ChannelId);
            }
        }
    }

    private async Task PushVideoAndUpdateDatabaseAsync(YoutubeVideo video, YoutubeSubscribeEntity record, CancellationToken ct)
    {
        bool pushed = await PushMsgAsync(video, record, ct);
        if (pushed)
        {
            Log.Information("Succeeded to push the youtube message from the user {Author}.\nChannelId: {channelId}",
                video.Author, video.ChannelId);
        }

        record.LastVideoId = video.Id;
        record.LastVideoPubTime = video.PubTime;
        await _repo.SaveAsync(ct);
        Log.Debug("Succeeded to updated the youtube user({user})'s record.\nChannelId: {channelId}",
            video.Author, video.ChannelId);
    }

    private async Task<bool> PushMsgAsync<T>(T item, YoutubeSubscribeEntity record, CancellationToken ct = default)
        where T : YoutubeItem
    {
        (string title, string text, string imgUrl) = ItemToStr(item);
        string uploadedImgUrl = await _cosSvc.UploadImageAsync(imgUrl, ct);

        List<YoutubeSubscribeConfigEntity> configs = await _configRepo.GetAllAsync(
            item.ChannelId, ct: ct);

        bool pushed = false;
        List<Task> tasks = new();
        foreach (QQChannelSubscribeEntity channel in record.QQChannels)
        {
            if (!await _qbSvc.ExistChannelAsync(channel.ChannelId))
            {
                Log.Warning("The channel {channelName}(id: {channelId}) doesn't exist!",
                    channel.ChannelName, channel.ChannelId);
                continue;
            }

            YoutubeSubscribeConfigEntity config = configs.First(c => c.QQChannel.ChannelId == channel.ChannelId);

            if (item.Type == YoutubeTypeEnum.Video && !config.VideoPush)
                continue;
            if (item.Type == YoutubeTypeEnum.Live && !config.LivePush)
                continue;
            if (item.Type == YoutubeTypeEnum.UpcomingLive && !config.UpcomingLivePush)
                continue;
            if (config.ArchivePush && record.AllArchiveVideoIds.Contains(item.ChannelId) == false)
                continue;

            tasks.Add(_qbSvc.PushCommonMsgAsync(channel.ChannelId, $"{title}\n\n{text}", uploadedImgUrl, ct));
            pushed = true;
        }

        await Task.WhenAll(tasks);
        return pushed;
    }

    private (string title, string text, string imgUrl) ItemToStr<T>(T item)
        where T : YoutubeItem
    {
        return item switch
        {
            YoutubeLive live => (live.Type is YoutubeTypeEnum.Live ? $"【油管开播】来自 {live.Author}" : $"【油管预定开播】来自 {live.Author}", LiveToStr(live), live.ThumbnailUrl),
            YoutubeVideo video => ($"【新油管视频】来自 {video.Author}", VideoToStr(video), video.ThumbnailUrl),
            _ => throw new NotSupportedException($"Not supported item type {item.GetType().Name}")
        };
    }

    private string LiveToStr(YoutubeLive live)
    {
        if (live.Type == YoutubeTypeEnum.Live)
        {
            string actualStartTime = TimeZoneInfo
            .ConvertTimeFromUtc((DateTime)live.ActualStartTime!, TimeUtil.CST)
            .ToString("yyyy-MM-dd HH:mm:ss zzz");

            return $"""
                直播标题：{live.Title}
                开播时间：{actualStartTime}
                直播间链接：{live.Url.AddRedirectToUrls(_redirectUrl)}
                直播间封面：
                """;
        }
        else
        {
            string scheduledStartTime = TimeZoneInfo
            .ConvertTimeFromUtc((DateTime)live.ScheduledStartTime!, TimeUtil.CST)
            .ToString("yyyy-MM-dd HH:mm:ss zzz");

            return $"""
                直播标题：{live.Title}
                预定开播时间：{scheduledStartTime}
                直播间链接：{live.Url.AddRedirectToUrls(_redirectUrl)}
                直播间封面：
                """;
        }
    }

    private string VideoToStr(YoutubeVideo item)
    {
        string pubTimeStr = TimeZoneInfo
            .ConvertTimeFromUtc(item.PubTime, TimeUtil.CST)
            .ToString("yyyy-MM-dd HH:mm:ss zzz");

        return $"""
            视频标题：{item.Title}
            视频发布时间：{pubTimeStr}
            视频链接：{item.Url.AddRedirectToUrls(_redirectUrl)}
            视频封面：
            """;
    }
}
