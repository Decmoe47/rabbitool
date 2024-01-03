using Autofac.Annotation;
using Autofac.Annotation.Condition;
using Coravel.Invocable;
using Coravel.Scheduling.Schedule.Interfaces;
using Rabbitool.Api;
using Rabbitool.Common.Configs;
using Rabbitool.Common.Provider;
using Rabbitool.Common.Util;
using Rabbitool.Model.DTO.Youtube;
using Rabbitool.Model.Entity.Subscribe;
using Rabbitool.Repository.Subscribe;
using Serilog;

namespace Rabbitool.Plugin;

[ConditionalOnProperty("youtube")]
[Component(AutofacScope = AutofacScope.SingleInstance)]
public class YoutubePlugin(
    QQBotApi qqBotApi,
    YoutubeApi youtubeApi,
    YoutubeSubscribeRepository repo,
    YoutubeSubscribeConfigRepository configRepo,
    CommonConfig commonConfig,
    ICancellationTokenProvider ctp) : IScheduledPlugin, ICancellableInvocable
{
    private readonly Dictionary<string, Dictionary<DateTime, YoutubeVideo>> _storedVideos = new();
    public CancellationToken CancellationToken { get; set; }
    public string Name => "youtube";

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
                .PreventOverlapping("YoutubePlugin");
        };
    }

    private async Task CheckAllAsync()
    {
        if (CancellationToken.IsCancellationRequested)
            return;

        List<YoutubeSubscribeEntity> records = await repo.GetAllAsync(true, ctp.Token);
        if (records.Count == 0)
        {
            Log.Verbose("[Youtube] There isn't any youtube subscribe yet!");
            return;
        }

        List<Task> tasks = [];
        foreach (YoutubeSubscribeEntity record in records)
        {
            tasks.Add(CheckAsync(record));
            tasks.Add(CheckUpcomingLiveAsync(record));
        }

        await Task.WhenAll(tasks);
    }

    private async Task CheckAsync(YoutubeSubscribeEntity record)
    {
        try
        {
            YoutubeItem item = await youtubeApi.GetLatestVideoOrLiveAsync(record.ChannelId, ctp.Token);
            DateTime now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeUtil.CST);

            switch (item)
            {
                case YoutubeLive live:
                    switch (live.Type)
                    {
                        case YoutubeTypeEnum.UpcomingLive when !record.AllUpcomingLiveRoomIds.Contains(live.Id):
                            record.AllUpcomingLiveRoomIds.Add(live.Id);
                            await repo.SaveAsync(ctp.Token);
                            Log.Debug(
                                "[Youtube] Succeeded to updated the youtube user({user})'s record.\nChannelId: {channelId}",
                                live.Author, live.ChannelId);

                            await PushUpcomingLiveAsync(live, record);
                            break;
                        case YoutubeTypeEnum.Live when live.Id != record.LastLiveRoomId
                                                       && !record.AllUpcomingLiveRoomIds.Contains(live.Id):
                            await PushLiveAndUpdateDatabaseAsync(live, record);
                            break;
                    }

                    break;

                case YoutubeVideo video:
                    if (video.PubTime <= record.LastVideoPubTime)
                    {
                        Log.Debug("[Youtube] No new youtube video from the youtube user {name}(channelId: {channelId})",
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
                            "[Youtube] Youtube video message of the user {name}(channelId: {channelId} is skipped because it's curfew time now.",
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
                            await PushVideoAndUpdateDatabaseAsync(storedVideos[pubTime], record);
                            _storedVideos[video.ChannelId].Remove(pubTime);
                        }

                        return;
                    }

                    await PushVideoAndUpdateDatabaseAsync(video, record);
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
            Log.Error(ex, "[Youtube] Failed to push youtube message!\nName: {name}\nChannelId: {channelId}",
                record.Name, record.ChannelId);
        }
    }

    private async Task CheckUpcomingLiveAsync(YoutubeSubscribeEntity record)
    {
        List<string> allUpcomingLiveRoomIdsTmp = record.AllUpcomingLiveRoomIds;
        List<string> roomIdsToRemove = [];
        foreach (string roomId in allUpcomingLiveRoomIdsTmp)
        {
            if (await youtubeApi.IsStreamingAsync(roomId, ctp.Token) is not { } live)
                continue;
            Log.Debug("[Youtube] Youtube upcoming live (roomId: {roomId}) starts streaming.", roomId);
            await PushLiveAndUpdateDatabaseAsync(live, record, false);
            roomIdsToRemove.Add(roomId);
        }

        roomIdsToRemove.ForEach(roomId => record.AllUpcomingLiveRoomIds.Remove(roomId));
        await repo.SaveAsync(ctp.Token);
    }

    private async Task PushLiveAndUpdateDatabaseAsync(
        YoutubeLive live, YoutubeSubscribeEntity record, bool saving = true)
    {
        DateTime now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeUtil.CST);
        if (now.Hour is >= 0 and <= 5)
        {
            Log.Debug(
                "[Youtube] Youtube live message of the user {name}(channelId: {channelId} is skipped because it's curfew time now.",
                live.Author, live.ChannelId);
        }
        else
        {
            await PushMsgAsync(live, record);
            Log.Information(
                "[Youtube] Succeeded to push the youtube live message from the user {Author}.\nChannelId: {channelId}",
                live.Author, live.ChannelId);
        }

        record.LastLiveRoomId = live.Id;
        record.LastLiveStartTime = (DateTime)live.ActualStartTime!;
        record.AllArchiveVideoIds.Add(live.Id);

        if (record.AllArchiveVideoIds.Count > 5)
            record.AllArchiveVideoIds.RemoveAt(0);

        if (saving)
            await repo.SaveAsync(ctp.Token);
        Log.Debug("[Youtube] Succeeded to updated the youtube user({user})'s record.\nChannelId: {channelId}",
            live.Author, live.ChannelId);
    }

    private async Task PushUpcomingLiveAsync(
        YoutubeLive live, YoutubeSubscribeEntity record)
    {
        DateTime now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeUtil.CST);
        if (now.Hour is >= 0 and <= 5)
            Log.Debug(
                "[Youtube] Youtube upcoming live message of the user {name}(channelId: {channelId} is skipped because it's curfew time now.",
                live.Author, live.ChannelId);
        else
            await PushMsgAsync(live, record);
    }

    private async Task PushVideoAndUpdateDatabaseAsync(
        YoutubeVideo video, YoutubeSubscribeEntity record)
    {
        await PushMsgAsync(video, record);

        record.LastVideoId = video.Id;
        record.LastVideoPubTime = video.PubTime;
        await repo.SaveAsync(ctp.Token);
        Log.Debug("[Youtube] Succeeded to updated the youtube user({user})'s record.\nChannelId: {channelId}",
            video.Author, video.ChannelId);
    }

    private async Task PushMsgAsync<T>(T item, YoutubeSubscribeEntity record)
        where T : YoutubeItem
    {
        (string title, string text, string imgUrl) = ItemToStr(item);

        List<YoutubeSubscribeConfigEntity> configs = await configRepo.GetAllAsync(item.ChannelId, ct: ctp.Token);
        foreach (QQChannelSubscribeEntity channel in record.QQChannels)
        {
            if (!await qqBotApi.ExistChannelAsync(channel.ChannelId))
            {
                Log.Warning("[Youtube] The channel {channelName}(id: {channelId}) doesn't exist!",
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

            await qqBotApi.PushCommonMsgAsync(channel.ChannelId, channel.ChannelName, $"{title}\n\n{text}", imgUrl,
                ctp.Token);
            switch (item.Type)
            {
                case YoutubeTypeEnum.Video:
                    Log.Information(
                        "[Youtube] Succeeded to push the youtube message from the user {Author}.\nChannelId: {channelId}",
                        item.Author, item.ChannelId);
                    break;
                case YoutubeTypeEnum.Live:
                case YoutubeTypeEnum.UpcomingLive:
                    Log.Information(
                        "[Youtube] Succeeded to push the youtube live message from the user {Author}.\nChannelId: {channelId}",
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
            // string actualStartTime = TimeZoneInfo
            //     .ConvertTimeFromUtc((DateTime)live.ActualStartTime!, TimeUtil.CST)
            //     .ToString("yyyy-MM-dd HH:mm:ss");
            return $"""
                    {live.Title}
                    ——————————
                    链接：{live.Url.AddRedirectToUrls(commonConfig.RedirectUrl)}
                    """;

        string scheduledStartTime = TimeZoneInfo
            .ConvertTimeFromUtc((DateTime)live.ScheduledStartTime!, TimeUtil.CST)
            .ToString("yyyy-MM-dd HH:mm:ss");

        return $"""
                {live.Title}
                ——————————
                预定开播时间：{scheduledStartTime}
                链接：{live.Url.AddRedirectToUrls(commonConfig.RedirectUrl)}
                """;
    }

    private string VideoToStr(YoutubeVideo video)
    {
        // string pubTimeStr = TimeZoneInfo
        //     .ConvertTimeFromUtc(video.PubTime, TimeUtil.CST)
        //     .ToString("yyyy-MM-dd HH:mm:ss");

        return $"""
                {video.Title}
                ——————————
                链接：{video.Url.AddRedirectToUrls(commonConfig.RedirectUrl)}
                """;
    }
}