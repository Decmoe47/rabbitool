using Flurl;
using Flurl.Http;
using Newtonsoft.Json.Linq;
using Rabbitool.Common.Exception;
using Rabbitool.Common.Extension;
using Rabbitool.Common.Util;
using Rabbitool.Model.DTO.Bilibili;
using Serilog;

namespace Rabbitool.Service;

public class BilibiliService
{
    private readonly string _userAgent;
    private readonly CookieJar _jar;
    private readonly LimiterUtil _limiter = LimiterCollection.BilibiliLimiter;

    public BilibiliService(string userAgent)
    {
        _userAgent = userAgent;
        _jar = new CookieJar();
    }

    public async Task RefreshCookiesAsync(CancellationToken ct = default)
    {
        _ = await "https://bilibili.com"
            .WithTimeout(10)
            .WithCookies(_jar)
            .GetAsync(ct);
    }

    public async Task<Live?> GetLiveAsync(uint uid, CancellationToken ct = default)
    {
        _limiter.Wait(ct: ct);

        string resp = await "https://api.bilibili.com/x/space/acc/info"
                .SetQueryParam("mid", uid)
                .WithTimeout(10)
                .WithCookies(_jar)
                .WithHeader("User-Agent", _userAgent)
                .GetStringAsync(ct);
        JObject body = JObject.Parse(resp).RemoveNullAndEmptyProperties();
        if ((int?)body["code"] is int code and not 0)
            throw new BilibiliApiException($"Failed to get the info from the bilibili user(uid: {uid})!", code, body);

        string uname = (string)body["data"]!["name"]!;

        if ((int?)body["data"]?["live_room"]?["roomStatus"] is null or 0)
        {
            Log.Debug("The bilibili user {uname}(uid: {uid}) hasn't live room yet!", uname, uid);
            return null;
        }

        uint roomId = (uint)body["data"]!["live_room"]!["roomid"]!;

        string resp2 = await "https://api.live.bilibili.com/xlive/web-room/v1/index/getInfoByRoom"
            .SetQueryParam("room_id", roomId)
            .WithTimeout(10)
            .WithCookies(_jar)
            .WithHeader("User-Agent", _userAgent)
            .GetStringAsync(ct);
        JObject body2 = JObject.Parse(resp2).RemoveNullAndEmptyProperties();
        if ((int?)body2["code"] is int code2 and not 0)
            throw new BilibiliApiException($"Failed to get the info from the bilibili user(uid: {uid})!", code2, body2);

        int liveStatus = (int)body2["data"]!["room_info"]!["live_status"]!;

        switch (liveStatus)
        {
            case (int)LiveStatusEnum.NoLiveStream:
                return new Live()
                {
                    Uid = uid,
                    Uname = uname,
                    RoomId = roomId,
                    LiveStatus = LiveStatusEnum.NoLiveStream,
                };

            case (int)LiveStatusEnum.Replay:
                return new Live()
                {
                    Uid = uid,
                    Uname = uname,
                    RoomId = roomId,
                    LiveStatus = LiveStatusEnum.Replay,
                };

            case (int)LiveStatusEnum.Streaming:
                DateTime liveStartTime = DateTimeOffset
                    .FromUnixTimeSeconds((long)body2["data"]!["room_info"]!["live_start_time"]!)
                    .UtcDateTime;
                return new Live()
                {
                    Uid = uid,
                    Uname = uname,
                    RoomId = roomId,
                    LiveStatus = LiveStatusEnum.Streaming,
                    Title = (string)body2["data"]!["room_info"]!["title"]!,
                    LiveStartTime = liveStartTime,
                    CoverUrl = (string)body2["data"]!["room_info"]!["cover"]!
                };

            default:
                throw new NotSupportedException(
                    $"Unknow live status {liveStatus} from the bilibili user {uname}(uid: {uid})");
        }
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="uid"></param>
    /// <param name="offsetDynamic"></param>
    /// <param name="needTop"></param>
    /// <returns>用户未存在动态时返回<c>null</c>，并已输出log</returns>
    /// <exception cref="BilibiliApiException"></exception>
    /// <exception cref="NotSupportedException"></exception>
    public async Task<BaseDynamicDTO?> GetLatestDynamicAsync(
        uint uid,
        int offsetDynamic = 0,
        int needTop = 0,
        CancellationToken ct = default)
    {
        _limiter.Wait(ct: ct);

        string resp = await "https://api.vc.bilibili.com/dynamic_svr/v1/dynamic_svr/space_history"
                .SetQueryParam("offsetDynamic", offsetDynamic)
                .SetQueryParam("needTop", needTop)
                .SetQueryParam("host_uid", uid)
                .WithTimeout(10)
                .WithCookies(_jar)
                .WithHeader("User-Agent", _userAgent)
                .GetStringAsync(ct);
        JObject body = JObject.Parse(resp).RemoveNullAndEmptyProperties();
        if ((int?)body["code"] is int code and not 0)
            throw new BilibiliApiException($"Failed to get the info from the bilibili user(uid: {uid})!", code, body);

        JToken? dy = body["data"]?["cards"]?[0];
        if (dy == null)
        {
            Log.Debug("The {uid} is invaild or the user hasn't any dynamic yet!", uid);
            return null;
        }
        UnmarshalCard(dy);
        int type = (int)dy["desc"]!["type"]!;

        return type switch
        {
            (int)DynamicTypeEnum.TextOnly or (int)DynamicTypeEnum.WithImage or (int)DynamicTypeEnum.WebActivity
                => ToCommonDynamic(dy),
            (int)DynamicTypeEnum.Video => ToVideoDynamic(dy),
            (int)DynamicTypeEnum.Article => ToArticleDynamic(dy),
            (int)DynamicTypeEnum.Forward => await ToForwardDynamicAsync(dy, ct),
            _ => throw new NotSupportedException($"Not supported dynamic type {type}\nDynamic: {dy}"),
        };
    }

    private static void UnmarshalCard(JToken dy)
    {
        uint uid = (uint?)dy["desc"]?["uid"] ?? 0;
        string cardStr = (string?)dy["card"]
            ?? throw new JsonUnmarshalException($"Failed to unmarshal the card!(uid: {uid})");
        JObject card = JObject.Parse(cardStr).RemoveNullAndEmptyProperties();

        if ((string?)card["origin"] is string origin)
            card["origin"] = JObject.Parse(origin).RemoveNullAndEmptyProperties();
        if ((string?)card["origin_extend_json"] is string originExtendJson)
            card["origin_extend_json"] = JObject.Parse(originExtendJson).RemoveNullAndEmptyProperties();
        if ((string?)card["ctrl"] is string ctrl)
            card["ctrl"] = JObject.Parse(ctrl).RemoveNullAndEmptyProperties();

        dy["card"] = card;
    }

    private static ReserveDTO? GetReserve(JToken dy)
    {
        if ((int?)dy["display"]?["origin"]?["add_on_card_info"]?[0]?["add_on_card_show_type"] == 6)
        {
            return new ReserveDTO()
            {
                Title = (string)dy["display"]!["origin"]!["add_on_card_info"]![0]!["reserve_attach_card"]!["title"]!,
                StartTime = DateTimeOffset.FromUnixTimeSeconds(
                    (long)dy["display"]!["origin"]!["add_on_card_info"]![0]!["reserve_attach_card"]!["livePlanStartTime"]!)
                    .UtcDateTime
            };
        }
        else if ((int?)dy["display"]?["add_on_card_info"]?[0]?["add_on_card_show_type"] == 6)
        {
            return new ReserveDTO()
            {
                Title = (string)dy["display"]!["add_on_card_info"]![0]!["reserve_attach_card"]!["title"]!,
                StartTime = DateTimeOffset.FromUnixTimeSeconds(
                    (long)dy["display"]!["add_on_card_info"]![0]!["reserve_attach_card"]!["livePlanStartTime"]!)
                    .UtcDateTime
            };
        }
        else
        {
            return null;
        }
    }

    private static CommonDynamicDTO ToCommonDynamic(JToken dy)
    {
        List<string>? imgUrls = null;
        if ((JArray?)dy["card"]?["item"]?["pictures"] is JArray pics)
        {
            imgUrls = new List<string>();
            foreach (JToken? img in pics)
            {
                if ((string?)img["img_src"] is string src)
                    imgUrls.Add(src);
            }
        }

        return new CommonDynamicDTO()
        {
            Uid = (uint)dy["desc"]!["uid"]!,
            Uname = (string)dy["desc"]!["user_profile"]!["info"]!["uname"]!,
            DynamicType = imgUrls == null ? DynamicTypeEnum.TextOnly : DynamicTypeEnum.WithImage,
            DynamicId = (string)dy["desc"]!["dynamic_id_str"]!,
            DynamicUrl = "https://t.bilibili.com/" + (string)dy["desc"]!["dynamic_id_str"]!,
            DynamicUploadTime = DateTimeOffset.FromUnixTimeSeconds((long)dy["desc"]!["timestamp"]!).UtcDateTime,
            Text = (string?)dy["card"]?["item"]?["description"]
                ?? (string)dy["card"]!["item"]!["content"]!,
            ImageUrls = imgUrls,
            Reserve = GetReserve(dy)
        };
    }

    private static VideoDynamicDTO ToVideoDynamic(JToken dy)
    {
        return new VideoDynamicDTO()
        {
            Uid = (uint)dy["desc"]!["uid"]!,
            Uname = (string)dy["desc"]!["user_profile"]!["info"]!["uname"]!,
            DynamicType = DynamicTypeEnum.Video,
            DynamicId = (string)dy["desc"]!["dynamic_id_str"]!,
            DynamicUrl = "https://t.bilibili.com/" + (string)dy["desc"]!["dynamic_id_str"]!,
            DynamicUploadTime = DateTimeOffset.FromUnixTimeSeconds((long)dy["desc"]!["timestamp"]!).UtcDateTime,
            DynamicText = (string)dy["card"]!["dynamic"]!,
            VideoTitle = (string)dy["card"]!["title"]!,
            VideoThumbnailUrl = (string)dy["card"]!["pic"]!,
            VideoUrl = (string)dy["card"]!["short_link"]!
        };
    }

    private static ArticleDynamicDTO ToArticleDynamic(JToken dy)
    {
        return new ArticleDynamicDTO()
        {
            Uid = (uint)dy["desc"]!["uid"]!,
            Uname = (string)dy["desc"]!["user_profile"]!["info"]!["uname"]!,
            DynamicType = DynamicTypeEnum.Article,
            DynamicId = (string)dy["desc"]!["dynamic_id_str"]!,
            DynamicUrl = "https://t.bilibili.com/" + (string)dy["desc"]!["dynamic_id_str"]!,
            DynamicUploadTime = DateTimeOffset.FromUnixTimeSeconds((long)dy["desc"]!["timestamp"]!).UtcDateTime,
            ArticleTitle = (string)dy["card"]!["title"]!,
            ArticleThumbnailUrl = (string)dy["card"]!["image_urls"]![0]!,
            ArticleUrl = "https://www.bilibili.com/read/cv" + (string)dy["card"]!["id"]!
        };
    }

    private async Task<ForwardDynamicDTO> ToForwardDynamicAsync(JToken dy, CancellationToken ct = default)
    {
        DateTime dynamicUploadTime = DateTimeOffset.FromUnixTimeSeconds((long)dy["desc"]!["timestamp"]!).UtcDateTime;
        string dynamicText = (string)dy["card"]!["item"]!["content"]!;
        DynamicTypeEnum dynamicType = IsPureForwardDynamic(dynamicText)
            ? DynamicTypeEnum.PureForward : DynamicTypeEnum.Forward;

        if ((string?)dy["card"]!["item"]!["tips"] == "源动态已被作者删除")
        {
            return new ForwardDynamicDTO()
            {
                Uid = (uint)dy["desc"]!["uid"]!,
                Uname = (string)dy["desc"]!["user_profile"]!["info"]!["uname"]!,
                DynamicType = dynamicType,
                DynamicId = (string)dy["desc"]!["dynamic_id_str"]!,
                DynamicUrl = "https://t.bilibili.com/" + (string)dy["desc"]!["dynamic_id_str"]!,
                DynamicUploadTime = dynamicUploadTime,
                DynamicText = dynamicText,
                Origin = "源动态已被作者删除"
            };
        }

        object origin;
        uint originUid = (uint)dy["desc"]!["origin"]!["uid"]!;
        string originUname = await GetUnameAsync(originUid, ct);
        string originDynamicId = (string)dy["desc"]!["origin"]!["dynamic_id_str"]!;
        string originDynamicUrl = "https://t.bilibili.com/" + originDynamicId;
        DateTime originDynamicUploadTime = DateTimeOffset
            .FromUnixTimeSeconds((long)dy["desc"]!["origin"]!["timestamp"]!)
            .UtcDateTime;
        int originDynamicType = (int)dy["desc"]!["origin"]!["type"]!;

        switch (originDynamicType)
        {
            case (int)DynamicTypeEnum.TextOnly or (int)DynamicTypeEnum.WithImage or (int)DynamicTypeEnum.WebActivity:
                List<string>? imgUrls = null;
                if ((JArray?)dy["card"]?["origin"]?["item"]?["pictures"] is JArray pics)
                {
                    imgUrls = new List<string>();
                    foreach (JToken? img in pics)
                    {
                        if ((string?)img["img_src"] is string src)
                            imgUrls.Add(src);
                    }
                }

                origin = new CommonDynamicDTO()
                {
                    Uid = originUid,
                    Uname = originUname,
                    DynamicType = imgUrls == null ? DynamicTypeEnum.TextOnly : DynamicTypeEnum.WithImage,
                    DynamicId = originDynamicId,
                    DynamicUrl = originDynamicUrl,
                    DynamicUploadTime = originDynamicUploadTime,
                    Text = (string?)dy["card"]?["origin"]?["item"]!["description"]
                        ?? (string)dy["card"]!["origin"]!["item"]!["content"]!,
                    ImageUrls = imgUrls,
                    Reserve = GetReserve(dy),
                };
                break;

            case (int)DynamicTypeEnum.Video:
                origin = new VideoDynamicDTO()
                {
                    Uid = originUid,
                    Uname = originUname,
                    DynamicType = DynamicTypeEnum.Video,
                    DynamicId = originDynamicId,
                    DynamicUrl = originDynamicUrl,
                    DynamicUploadTime = originDynamicUploadTime,
                    DynamicText = (string)dy["card"]!["origin"]!["dynamic"]!,
                    VideoTitle = (string)dy["card"]!["origin"]!["title"]!,
                    VideoThumbnailUrl = (string)dy["card"]!["origin"]!["pic"]!,
                    VideoUrl = (string)dy["card"]!["origin"]!["short_link"]!
                };
                break;

            case (int)DynamicTypeEnum.Article:
                origin = new ArticleDynamicDTO()
                {
                    Uid = originUid,
                    Uname = originUname,
                    DynamicType = DynamicTypeEnum.Video,
                    DynamicId = originDynamicId,
                    DynamicUrl = originDynamicUrl,
                    DynamicUploadTime = originDynamicUploadTime,
                    ArticleTitle = (string)dy["card"]!["origin"]!["title"]!,
                    ArticleThumbnailUrl = (string)dy["card"]!["origin"]!["origin_image_urls"]![0]!,
                    ArticleUrl = "https://www.bilibili.com/read/cv" + (string)dy["card"]!["origin"]!["id"]!
                };
                break;

            case (int)DynamicTypeEnum.LiveCard:
                origin = new LiveCardDynamicDTO()
                {
                    Uid = originUid,
                    Uname = originUname,
                    DynamicType = DynamicTypeEnum.LiveCard,
                    DynamicId = originDynamicId,
                    DynamicUrl = originDynamicUrl,
                    DynamicUploadTime = originDynamicUploadTime,
                    RoomId = (uint)dy["card"]!["origin"]!["roomid"]!,
                    LiveStatus = (LiveStatusEnum)(int)dy["card"]!["origin"]!["live_status"]!,
                    LiveStartTime = DateTimeOffset
                        .FromUnixTimeSeconds((long)dy["card"]!["origin"]!["first_live_time"]!)
                        .UtcDateTime,
                    Title = (string)dy["card"]!["origin"]!["title"]!,
                    CoverUrl = (string)dy["card"]!["origin"]!["cover"]!
                };
                break;

            default:
                throw new NotSupportedException(
                    $"Not supported origin dynamic type {originDynamicType} for origin dynamic from the bilibili user (uid: {originUid})!");
        }

        return new ForwardDynamicDTO()
        {
            Uid = (uint)dy["desc"]!["uid"]!,
            Uname = (string)dy["desc"]!["user_profile"]!["info"]!["uname"]!,
            DynamicType = dynamicType,
            DynamicId = (string)dy["desc"]!["dynamic_id_str"]!,
            DynamicUrl = "https://t.bilibili.com/" + (string)dy["desc"]!["dynamic_id_str"]!,
            DynamicUploadTime = dynamicUploadTime,
            DynamicText = dynamicText,
            Origin = origin,
        };
    }

    private async Task<string> GetUnameAsync(uint uid, CancellationToken ct = default)
    {
        _limiter.Wait(ct: ct);

        string resp = await "https://api.bilibili.com/x/space/acc/info"
            .SetQueryParam("mid", uid)
            .WithCookies(_jar)
            .WithTimeout(10)
            .WithHeader("User-Agent", _userAgent)
            .GetStringAsync(ct);
        JObject body = JObject.Parse(resp).RemoveNullAndEmptyProperties();
        if ((int?)body["code"] is int code && code != 0)
            throw new BilibiliApiException($"Failed to get the uname of uid {uid}", code, body);

        return (string)body["data"]!["name"]!;
    }

    private static bool IsPureForwardDynamic(string dynamicText)
    {
        return dynamicText == "转发动态" || dynamicText.Split("//")[0].StartsWith("@");
    }
}
