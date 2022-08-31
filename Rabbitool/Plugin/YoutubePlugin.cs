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

    private Dictionary<string, Dictionary<DateTime, YoutubeVideo>> _storedVideos = new();

    public YoutubePlugin(
        QQBotService qbSvc,
        CosService cosSvc,
        string dbPath,
        string redirectUrl,
        string userAgent) : base(qbSvc, cosSvc, dbPath, redirectUrl, userAgent)
    {
        _svc = new YoutubeService();

        SubscribeDbContext dbCtx = new(_dbPath);
        _repo = new YoutubeSubscribeRepository(dbCtx);
        _configRepo = new YoutubeSubscribeConfigRepository(dbCtx);
    }

    public async Task CheckAllAsync(CancellationToken cancellationToken = default)
    {
        List<YoutubeSubscribeEntity> records = await _repo.GetAllAsync(true, cancellationToken);
        if (records.Count == 0)
        {
            Log.Debug("There isn't any youtube subscribe yet!");
            return;
        }

        List<Task> tasks = new();
        foreach (YoutubeSubscribeEntity record in records)
            tasks.Add(CheckAsync(record, cancellationToken));
        await Task.WhenAll(tasks);
    }

    private async Task CheckAsync(YoutubeSubscribeEntity record, CancellationToken cancellationToken = default)
    {
        try
        {
            YoutubeBase item = await _svc.GetLatestVideoOrLiveAsync(record.ChannelId, cancellationToken);

            async Task FnAsync(YoutubeVideo video)
            {
                bool pushed = await PushMsgAsync(video, record, cancellationToken);
                if (pushed)
                {
                    Log.Information("Succeeded to push the youtube message from the user {Author}.\nChannelId: {channelId}",
                        video.Author, video.ChannelId);
                }

                record.LastVideoOrLiveId = video.Id;
                record.LastVideoOrLiveTime = video.PubTime;
                await _repo.SaveAsync(cancellationToken);
                Log.Debug("Succeeded to updated the youtube user({user})'s record.\nChannelId: {channelId}",
                    video.Author, video.ChannelId);
            }

            DateTime now = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.Now, "China Standard Time");

            switch (item)
            {
                case YoutubeLive lItem:
                    if (lItem.Id != record.LastVideoOrLiveId)
                    {
                        if (now.Hour >= 0 && now.Hour <= 6)
                        {
                            Log.Debug("Youtube video message of the user {name}(channelId: {channelId} is skipped because it's curfew time now.",
                                lItem.Author, lItem.ChannelId);
                            return;
                        }

                        await PushMsgAsync(lItem, record, cancellationToken);
                        Log.Information("Succeeded to push the youtube message from the user {Author}.\nChannelId: {channelId}",
                            lItem.Author, lItem.ChannelId);

                        record.LastVideoOrLiveId = lItem.Id;
                        record.LastVideoOrLiveTime = lItem.LiveStartTime;
                        record.AllArchiveVideoIds = record.AllArchiveVideoIds is null
                            ? new List<string>() { lItem.Id }
                            : (List<string>)record.AllArchiveVideoIds.Append(lItem.Id);
                        await _repo.SaveAsync(cancellationToken);
                        Log.Debug("Succeeded to updated the youtube user({user})'s record.\nChannelId: {channelId}",
                            lItem.Author, lItem.ChannelId);
                    }
                    break;

                case YoutubeVideo vItem:
                    if (vItem.PubTime <= record.LastVideoOrLiveTime)
                    {
                        Log.Debug("No new youtube video from the youtube user {name}(channelId: {channelId})",
                            vItem.Author, vItem.ChannelId);
                        return;
                    }

                    if (now.Hour >= 0 && now.Hour <= 6)
                    {
                        if (!_storedVideos.ContainsKey(vItem.ChannelId) || !_storedVideos[vItem.ChannelId].ContainsKey(vItem.PubTime))
                            _storedVideos[vItem.ChannelId][vItem.PubTime] = vItem;
                        Log.Debug("Youtube video message of the user {name}(channelId: {channelId} is skipped because it's curfew time now.",
                            vItem.Author, vItem.ChannelId);
                        return;
                    }

                    if (_storedVideos.TryGetValue(vItem.ChannelId, out Dictionary<DateTime, YoutubeVideo>? storedVideos) && storedVideos != null && storedVideos.Count != 0)
                    {
                        List<DateTime> pubTimes = storedVideos.Keys.ToList();
                        pubTimes.Sort();
                        foreach (DateTime pubTime in pubTimes)
                        {
                            await FnAsync(storedVideos[pubTime]);
                            _storedVideos[vItem.ChannelId].Remove(pubTime);
                        }
                        return;
                    }

                    await FnAsync(vItem);

                    break;

                default:
                    throw new NotSupportedException($"Not supported type {item.GetType().Name} of the item!");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to push youtube message!\nName: {name}\nChannelId: {channelId}",
                record.Name, record.ChannelId);
        }
    }

    private async Task<bool> PushMsgAsync<T>(T item, YoutubeSubscribeEntity record, CancellationToken cancellationToken = default)
        where T : YoutubeBase
    {
        (string title, string text, string imgUrl) = ItemToStr(item);
        string uploadedImgUrl = await _cosSvc.UploadImageAsync(imgUrl, cancellationToken);

        List<YoutubeSubscribeConfigEntity> configs = await _configRepo.GetAllAsync(
            item.ChannelId, cancellationToken: cancellationToken);

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
            if (config.ArchivePush && record.AllArchiveVideoIds?.Contains(item.ChannelId) == false)
                continue;

            tasks.Add(_qbSvc.PushCommonMsgAsync(channel.ChannelId, $"{title}\n\n{text}", uploadedImgUrl));
            pushed = true;
        }

        await Task.WhenAll(tasks);
        return pushed;
    }

    private (string title, string text, string imgUrl) ItemToStr<T>(T item)
        where T : YoutubeBase
    {
        return item switch
        {
            YoutubeLive lItem => ($"【开播】来自 {lItem.Author}", LiveToStr(lItem), lItem.ThumbnailUrl),
            YoutubeVideo vItem => ($"【新视频】来自 {vItem.Author}", VideoToStr(vItem), vItem.ThumbnailUrl),
            _ => throw new NotSupportedException($"Not supported item type {item.GetType().Name}")
        };
    }

    private string LiveToStr(YoutubeLive item)
    {
        string liveStartTimeStr = TimeZoneInfo
            .ConvertTimeBySystemTimeZoneId(item.LiveStartTime, "China Standard Time")
            .ToString("yyyy-MM-dd HH:mm:ss zzz");

        return $@"直播标题：{item.Title}
开播时间：{liveStartTimeStr}
直播间链接：{item.Url.AddRedirectToUrls(_redirectUrl)}
直播间封面：";
    }

    private string VideoToStr(YoutubeVideo item)
    {
        string pubTimeStr = TimeZoneInfo
            .ConvertTimeBySystemTimeZoneId(item.PubTime, "China Standard Time")
            .ToString("yyyy-MM-dd HH:mm:ss zzz");

        return $@"视频标题：{item.Title}
视频发布时间：{pubTimeStr}
视频链接：{item.Url.AddRedirectToUrls(_redirectUrl)}
视频封面：";
    }
}
