using Coravel;
using Rabbitool.Common.Util;
using Rabbitool.Conf;
using Rabbitool.Model.DTO.Youtube;
using Rabbitool.Model.Entity.Subscribe;
using Rabbitool.Repository.Subscribe;
using Rabbitool.Service;
using Serilog;

namespace Rabbitool.Plugin;

public class YoutubePlugin : BasePlugin, IPlugin
{
    private readonly YoutubeSubscribeConfigRepository _configRepo;
    private readonly YoutubeSubscribeRepository _repo;

    private readonly Dictionary<string, Dictionary<DateTime, YoutubeVideo>> _storedVideos = new();
    private readonly YoutubeService _svc;

    public YoutubePlugin(QQBotService qbSvc, CosService cosSvc) : base(qbSvc, cosSvc)
    {
        _svc = new YoutubeService();

        SubscribeDbContext dbCtx = new(Configs.R.DbPath);
        _repo = new YoutubeSubscribeRepository(dbCtx);
        _configRepo = new YoutubeSubscribeConfigRepository(dbCtx);
    }

    public async Task InitAsync(IServiceProvider services, CancellationToken ct = default)
    {
        services.UseScheduler(scheduler =>
                scheduler
                    .ScheduleAsync(async () => await CheckAllAsync(ct))
                    .EverySeconds(5)
                    .PreventOverlapping("YoutubePlugin"))
            .OnError(ex => Log.Error(ex, "Exception from youtube plugin: {msg}", ex.Message));
    }

    public async Task CheckAllAsync(CancellationToken ct = default)
    {
        List<YoutubeSubscribeEntity> records = await _repo.GetAllAsync(true, ct);
        if (records.Count == 0)
        {
            Log.Verbose("There isn't any youtube subscribe yet!");
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
            YoutubeItem item = await _svc.GetLatestVideoOrLiveAsync(record.ChannelId, ct);
            DateTime now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeUtil.CST);

            switch (item)
            {
                case YoutubeLive live:
                    switch (live.Type)
                    {
                        case YoutubeTypeEnum.UpcomingLive when !record.AllUpcomingLiveRoomIds.Contains(live.Id):
                            record.AllUpcomingLiveRoomIds.Add(live.Id);
                            await _repo.SaveAsync(ct);
                            Log.Debug("Succeeded to updated the youtube user({user})'s record.\nChannelId: {channelId}",
                                live.Author, live.ChannelId);

                            await PushUpcomingLiveAsync(live, record, ct);
                            break;
                        case YoutubeTypeEnum.Live when live.Id != record.LastLiveRoomId
                                                       && !record.AllUpcomingLiveRoomIds.Contains(live.Id):
                            await PushLiveAndUpdateDatabaseAsync(live, record, ct: ct);
                            break;
                    }

                    break;

                case YoutubeVideo video:
                    if (video.PubTime <= record.LastVideoPubTime)
                    {
                        Log.Debug("No new youtube video from the youtube user {name}(channelId: {channelId})",
                            video.Author, video.ChannelId);
                        return;
                    }

                    if (now.Hour is >= 0 and <= 5)
                    {
                        if (!_storedVideos.ContainsKey(video.ChannelId))
                            _storedVideos[video.ChannelId] = new Dictionary<DateTime, YoutubeVideo>();
                        if (!_storedVideos[video.ChannelId].ContainsKey(video.PubTime))
                            _storedVideos[video.ChannelId][video.PubTime] = video;

                        Log.Debug(
                            "Youtube video message of the user {name}(channelId: {channelId} is skipped because it's curfew time now.",
                            video.Author, video.ChannelId);
                        return;
                    }

                    if (_storedVideos.TryGetValue(video.ChannelId, out Dictionary<DateTime, YoutubeVideo>? storedVideos)
                        && storedVideos.Count != 0)
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
            if (await _svc.IsStreamingAsync(roomId, ct) is not { } live)
                continue;
            Log.Debug("Youtube upcoming live (roomId: {roomId}) starts streaming.", roomId);
            await PushLiveAndUpdateDatabaseAsync(live, record, false, ct);
            record.AllUpcomingLiveRoomIds.Remove(roomId);
            await _repo.SaveAsync(ct);
        }
    }

    private async Task PushLiveAndUpdateDatabaseAsync(
        YoutubeLive live, YoutubeSubscribeEntity record, bool saving = true, CancellationToken ct = default)
    {
        DateTime now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeUtil.CST);
        if (now.Hour is >= 0 and <= 5)
        {
            Log.Debug(
                "Youtube live message of the user {name}(channelId: {channelId} is skipped because it's curfew time now.",
                live.Author, live.ChannelId);
        }
        else
        {
            await PushMsgAsync(live, record, ct);
            Log.Information(
                "Succeeded to push the youtube live message from the user {Author}.\nChannelId: {channelId}",
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
        if (now.Hour is >= 0 and <= 5)
            Log.Debug(
                "Youtube upcoming live message of the user {name}(channelId: {channelId} is skipped because it's curfew time now.",
                live.Author, live.ChannelId);
        else
            await PushMsgAsync(live, record, ct);
    }

    private async Task PushVideoAndUpdateDatabaseAsync(
        YoutubeVideo video, YoutubeSubscribeEntity record, CancellationToken ct = default)
    {
        await PushMsgAsync(video, record, ct);

        record.LastVideoId = video.Id;
        record.LastVideoPubTime = video.PubTime;
        await _repo.SaveAsync(ct);
        Log.Debug("Succeeded to updated the youtube user({user})'s record.\nChannelId: {channelId}",
            video.Author, video.ChannelId);
    }

    private async Task PushMsgAsync<T>(T item, YoutubeSubscribeEntity record, CancellationToken ct = default)
        where T : YoutubeItem
    {
        (string title, string text, string imgUrl) = ItemToStr(item);

        List<YoutubeSubscribeConfigEntity> configs = await _configRepo.GetAllAsync(item.ChannelId, ct: ct);
        foreach (QQChannelSubscribeEntity channel in record.QQChannels)
        {
            if (!await QbSvc.ExistChannelAsync(channel.ChannelId))
            {
                Log.Warning("The channel {channelName}(id: {channelId}) doesn't exist!",
                    channel.ChannelName, channel.ChannelId);
                continue;
            }

            YoutubeSubscribeConfigEntity config = configs.First(c => c.QQChannel.ChannelId == channel.ChannelId);

            switch (item.Type)
            {
                case YoutubeTypeEnum.Video when !config.VideoPush:
                case YoutubeTypeEnum.Live when !config.LivePush:
                case YoutubeTypeEnum.UpcomingLive when !config.UpcomingLivePush:
                    continue;
            }

            if (config.ArchivePush && record.AllArchiveVideoIds.Contains(item.ChannelId) == false)
                continue;

            await QbSvc.PushCommonMsgAsync(channel.ChannelId, channel.ChannelName, $"{title}\n\n{text}", imgUrl, ct);
            switch (item.Type)
            {
                case YoutubeTypeEnum.Video:
                    Log.Information(
                        "Succeeded to push the youtube message from the user {Author}.\nChannelId: {channelId}",
                        item.Author, item.ChannelId);
                    break;
                case YoutubeTypeEnum.Live:
                case YoutubeTypeEnum.UpcomingLive:
                    Log.Information(
                        "Succeeded to push the youtube live message from the user {Author}.\nChannelId: {channelId}",
                        item.Author, item.ChannelId);
                    break;
            }
        }
    }

    private (string title, string text, string imgUrl) ItemToStr<T>(T item) where T : YoutubeItem
    {
        return item switch
        {
            YoutubeLive live => (live.Type == YoutubeTypeEnum.Live
                ? $"【油管开播】来自 {live.Author}"
                : $"【油管预定开播】来自 {live.Author}", LiveToStr(live), live.ThumbnailUrl),
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
                    标题：{live.Title}
                    开播时间：{actualStartTime}
                    链接：{live.Url.AddRedirectToUrls()}
                    """;
        }

        string scheduledStartTime = TimeZoneInfo
            .ConvertTimeFromUtc((DateTime)live.ScheduledStartTime!, TimeUtil.CST)
            .ToString("yyyy-MM-dd HH:mm:ss zzz");

        return $"""
                标题：{live.Title}
                预定开播时间：{scheduledStartTime}
                链接：{live.Url.AddRedirectToUrls()}
                """;
    }

    private string VideoToStr(YoutubeVideo video)
    {
        string pubTimeStr = TimeZoneInfo
            .ConvertTimeFromUtc(video.PubTime, TimeUtil.CST)
            .ToString("yyyy-MM-dd HH:mm:ss zzz");

        return $"""
                标题：{video.Title}
                发布时间：{pubTimeStr}
                链接：{video.Url.AddRedirectToUrls()}
                """;
    }
}