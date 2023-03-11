using Flurl.Http;
using Newtonsoft.Json.Linq;
using Rabbitool.Common.Exception;
using Rabbitool.Common.Extension;
using Rabbitool.Common.Util;
using Rabbitool.Model.DTO.Twitter;
using Serilog;

namespace Rabbitool.Service;

public class TwitterService
{
    private readonly string _apiV1_1Auth = "Bearer AAAAAAAAAAAAAAAAAAAAANRILgAAAAAAnNwIzUejRCOuH5E6I8xnZz4puTs%3D1Zv7ttfk8LF81IUq16cHjhLTvJu4FA33AGWWjCpTnA";
    private readonly string? _apiV2Token;
    private readonly string? _cookie;
    private readonly LimiterUtil _tweetApiLimiter = LimiterCollection.TwitterTweetApiLimiter;
    private readonly LimiterUtil _userApiLimiter = LimiterCollection.TwitterUserApiLimiter;
    private readonly string _userAgent;
    private readonly bool _usingApiV2;
    private readonly string? _xCsrfToken;

    public TwitterService(string userAgent)
    {
        _userAgent = userAgent;
        _usingApiV2 = false;
    }

    public TwitterService(string userAgent, string xCsrfToken, string cookie)
    {
        _userAgent = userAgent;
        _xCsrfToken = xCsrfToken;
        _cookie = cookie;
        _usingApiV2 = false;
    }

    public TwitterService(string userAgent, string apiV2Token)
    {
        _userAgent = userAgent;
        _apiV2Token = apiV2Token;
        _usingApiV2 = true;
    }

    public async Task<Tweet> GetLatestTweetAsync(string screenName, CancellationToken ct = default)
    {
        return _usingApiV2
            ? await GetLatestTweetByApiV2Async(screenName, ct)
            : await GetLatestTweetByApi1_1Async(screenName, ct);
    }

    /// <summary>
    /// 无需官方开发者账号。如果有x-csrf-token和cookie的话还能看得到r18推文
    /// </summary>
    private async Task<Tweet> GetLatestTweetByApi1_1Async(string screenName, CancellationToken ct = default)
    {
        _tweetApiLimiter.Wait(ct: ct);

        Dictionary<string, string> headers = new()
        {
            { "Authorization", _apiV1_1Auth },
            { "User-Agent", _userAgent },
        };
        if (_xCsrfToken is not null) headers.Add("x-csrf-token", _xCsrfToken);
        if (_cookie is not null) headers.Add("Cookie", _cookie);

        string resp = await "https://api.twitter.com/1.1/statuses/user_timeline.json"
            .WithTimeout(10)
            .WithHeaders(headers)
            .SetQueryParams(new Dictionary<string, string>()
            {
                { "screen_name", screenName },
                { "exclude_replies", "false" },
                { "include_rts", "true" },
                { "count", "5" },
            })
            .GetStringAsync(ct);
        JArray body = JArray.Parse(resp);
        JObject tweet = (JObject)body[0];

        Tweet? origin = null;
        List<string>? imgUrls = null;
        bool hasVideo = false;
        string text = (string)tweet["text"]!;
        TweetTypeEnum tweetType = TweetTypeEnum.Common;

        if ((JArray?)tweet["extended_entities"]?["media"] is JArray media)
            (text, imgUrls, hasVideo) = GetMediaByApi1_1(media, text);

        if ((JObject?)tweet["quoted_status"] is JObject quotedStatus)
        {
            origin = GetOriginTweetByApi1_1(quotedStatus);
            tweetType = TweetTypeEnum.Quote;
        }
        else if ((JObject?)tweet["retweeted_status"] is JObject retweetedStatus)
        {
            origin = GetOriginTweetByApi1_1(retweetedStatus);
            tweetType = TweetTypeEnum.RT;
        }

        return new()
        {
            Id = (string)tweet["id_str"]!,
            Type = tweetType,
            Author = (string)tweet["user"]!["name"]!,
            AuthorScreenName = screenName,
            Text = text,
            ImageUrls = imgUrls,
            HasVideo = hasVideo,
            Url = $"https://twitter.com/{screenName}/status/{(string)tweet["id_str"]!}",
            PubTime = DateTime
                .ParseExact((string)tweet["created_at"]!, "ddd MMM dd HH:mm:ss zz00 yyyy", null)
                .ToUniversalTime(),
            Origin = origin,
        };
    }

    private static Tweet GetOriginTweetByApi1_1(JObject origin)
    {
        List<string>? imgUrls = null;
        bool hasVideo = false;
        string text = (string)origin["text"]!;

        if ((JArray?)origin["extended_entities"]?["media"] is JArray media)
            (text, imgUrls, hasVideo) = GetMediaByApi1_1(media, text);

        return new()
        {
            Id = (string)origin["id_str"]!,
            Type = TweetTypeEnum.Common,
            Author = (string)origin["user"]!["name"]!,
            AuthorScreenName = (string)origin["user"]!["screen_name"]!,
            Text = text,
            ImageUrls = imgUrls,
            HasVideo = hasVideo,
            PubTime = DateTime
                .ParseExact((string)origin["created_at"]!, "ddd MMM dd HH:mm:ss zz00 yyyy", null)
                .ToUniversalTime(),
            Url = $"https://twitter.com/{(string)origin["user"]!["screen_name"]!}/status/{(string)origin["id_str"]!}",
        };
    }

    private static (string text, List<string>? imgUrls, bool hasVideo) GetMediaByApi1_1(JArray media, string text)
    {
        var imgUrls = new List<string>();
        bool hasVideo = false;

        foreach (JToken medium in media)
        {
            try
            {
                string mediumType = (string)medium["type"]!;
                switch (mediumType)
                {
                    case "photo":
                        text = text.Replace((string)medium["url"]!, "");
                        imgUrls.Add((string)medium["media_url_https"]!);
                        break;

                    case "video":
                        text = text.Replace((string)medium["url"]!, "");
                        hasVideo = true;
                        break;

                    default:
                        throw new NotSupportedException($"Unknown media type {mediumType}");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Failed to get the media!\nBody: {media}"); ;
                continue;
            }
        }

        return (text, imgUrls.Count != 0 ? imgUrls : null, hasVideo);
    }

    private async Task<Tweet> GetLatestTweetByApiV2Async(string screenName, CancellationToken ct = default)
    {
        (string userId, _) = await GetUserIdAsync(screenName, ct);

        _tweetApiLimiter.Wait(ct: ct);
        string resp = await $"https://api.twitter.com/2/users/{userId}/tweets"
            .WithTimeout(10)
            .WithOAuthBearerToken(_apiV2Token)
            .SetQueryParams(new Dictionary<string, string>()
            {
                { "exclude", "replies" },
                { "tweet.fields", "author_id,created_at,entities,in_reply_to_user_id,referenced_tweets,text" },
                { "expansions", "author_id,in_reply_to_user_id,referenced_tweets.id,referenced_tweets.id.author_id,attachments.media_keys" },
                { "user.fields", "username,name" },
                { "media.fields", "preview_image_url,type,url" },
                { "max_results", "5" }
            })
            .GetStringAsync(ct);
        JObject body = JObject.Parse(resp).RemoveNullAndEmptyProperties();

        JObject tweet = (JObject)body["data"]![0]!;
        string id = (string)tweet["id"]!;
        string text = (string)tweet["text"]!;

        List<string>? imgUrls = null;
        bool hasVideo = false;
        if ((JArray?)tweet["entities"]?["urls"] is JArray media)
            (text, imgUrls, hasVideo) = await GetMediaByApiV2Async(body, media, text, id, ct);

        TweetTypeEnum tweetType = TweetTypeEnum.Common;
        Tweet? origin = null;
        if ((string?)tweet["referenced_tweets"]?[0]?["id"] is string originId)
        {
            string originType = (string)tweet["referenced_tweets"]![0]!["type"]!;
            tweetType = originType switch
            {
                "quoted" => TweetTypeEnum.Quote,
                "retweeted" => TweetTypeEnum.RT,
                _ => throw new NotSupportedException($"Unknown origin type {originType}!\nTweet: {tweet}"),
            };

            origin = await GetOriginTweetByApiV2Async(body, originId, ct);
            text = text.Replace(origin.Url, "");
        }

        return new()
        {
            Id = id,
            Type = tweetType,
            Url = $"https://twitter.com/{screenName}/status/{id}",
            PubTime = ((DateTime)tweet["created_at"]!).ToUniversalTime(),
            Author = (string)body["includes"]!["users"]![0]!["name"]!,
            AuthorScreenName = screenName,
            Text = text,
            ImageUrls = imgUrls,
            HasVideo = hasVideo,
            Origin = origin,
        };
    }

    private async Task<(string text, List<string>? imgUrls, bool hasVideo)> GetMediaByApiV2Async(
        JObject body, JArray media, string text, string tweetId, CancellationToken ct)
    {
        bool hasVideo = false;
        List<string> imgUrls = new();

        foreach (JToken medium in media)
        {
            if ((string?)medium["expanded_url"] is string expandUrl)
                text = text.Replace((string)medium["url"]!, expandUrl);

            string? mediaKey = (string?)medium["media_key"];
            if (mediaKey is null)
                continue;

            try
            {
                if (mediaKey?.StartsWith("3_") is true)
                {
                    imgUrls.Add(await GetImageOrVideoThumbnailUrlAsync(body, mediaKey, tweetId, ct));
                    text = text.Replace((string)medium["expanded_url"]!, "");
                }
                else if (mediaKey?.StartsWith("7_") is true)
                {
                    hasVideo = true;
                    imgUrls.Add(await GetImageOrVideoThumbnailUrlAsync(body, mediaKey, tweetId, ct));
                    text = text.Replace((string)medium["expanded_url"]!, "");
                }
                else if (mediaKey?.StartsWith("13_") is true)    // 13应该是广告性质的视频
                {
                    hasVideo = true;
                    text = text.Replace((string)medium["expanded_url"]!, "");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to get the media!\nMediaKey: {mediaKey}", mediaKey ?? "null");
                continue;
            }
        }

        return (text, imgUrls.Count != 0 ? imgUrls : null, hasVideo);
    }

    private async Task<string> GetImageOrVideoThumbnailUrlAsync(
        JObject body, string mediaKey, string tweetId, CancellationToken ct)
    {
        foreach (JToken medium in (JArray)body["includes"]!["media"]!)
        {
            if ((string?)medium["media_key"] == mediaKey)
            {
                if (mediaKey.StartsWith("3_"))
                    return string.Join(".", ((string)medium["url"]!).Split('.')[..^1]) + "?format=jpg&name=large";    // 获取原始尺寸大小
                else if (mediaKey.StartsWith("7_"))
                    return string.Join(".", ((string)medium["preview_image_url"]!).Split('.')[..^1]) + "?format=jpg&name=large";
                else if (mediaKey.StartsWith("13_"))
                    return string.Join(".", ((string)medium["url"]!).Split('.')[..^1]) + "?format=jpg&name=large";
            }
        }

        _tweetApiLimiter.Wait(ct: ct);
        string resp = await $"https://api.twitter.com/2/tweets/{tweetId}"
            .WithTimeout(10)
            .WithOAuthBearerToken(_apiV2Token)
            .SetQueryParams(new Dictionary<string, string>()
            {
                { "tweet.fields", "author_id,created_at,entities,in_reply_to_user_id,referenced_tweets,text" },
                { "expansions", "author_id,in_reply_to_user_id,referenced_tweets.id,referenced_tweets.id.author_id,attachments.media_keys" },
                { "user.fields", "username,name" },
                { "media.fields", "preview_image_url,type,url" },
            })
            .GetStringAsync(ct);
        JObject json = JObject.Parse(resp);
        JObject img = (JObject)json["includes"]!["media"]!.First(m => (string)m["media_key"]! == mediaKey);
        return string.Join(".", ((string)img["url"]!).Split('.')[..^1]) + "?format=jpg&name=large";
    }

    private async Task<Tweet> GetOriginTweetByApiV2Async(
        JObject body, string originId, CancellationToken ct = default)
    {
        JArray origins = (JArray)body["includes"]!["tweets"]!;
        foreach (JToken origin in origins)
        {
            if ((string?)origin["id"] == originId)
            {
                (string author, string screenName) = await GetUserNameAsync(
                    (string)origin["author_id"]!, ct);
                string text = (string)origin["text"]!;

                List<string>? imgUrls = null;
                bool hasVideo = false;
                if ((JArray?)origin["entities"]?["urls"] is JArray media)
                    (text, imgUrls, hasVideo) = await GetMediaByApiV2Async(body, media, text, originId, ct);

                return new()
                {
                    Id = originId,
                    Type = TweetTypeEnum.Common,
                    Url = $"https://twitter.com/{screenName}/status/{originId}",
                    PubTime = ((DateTime)origin["created_at"]!).ToUniversalTime(),
                    Author = author,
                    AuthorScreenName = screenName,
                    Text = text,
                    ImageUrls = imgUrls,
                    HasVideo = hasVideo,
                };
            }
        }

        throw new NotFoundException($"Couldn't find the origin tweet!(originID: {originId})");
    }

    private async Task<(string userId, string name)> GetUserIdAsync(
        string screenName, CancellationToken ct = default)
    {
        _userApiLimiter.Wait(ct: ct);
        string resp = await $"https://api.twitter.com/2/users/by/username/{screenName}"
            .WithTimeout(10)
            .WithHeader("Authorization", $"Bearer {_apiV2Token}")
            .GetStringAsync(ct);
        JObject body = JObject.Parse(resp).RemoveNullAndEmptyProperties();

        return ((string)body["data"]!["id"]!, (string)body["data"]!["name"]!);
    }

    private async Task<(string name, string screenName)> GetUserNameAsync(
        string userId, CancellationToken ct = default)
    {
        _userApiLimiter.Wait(ct: ct);
        string resp = await $"https://api.twitter.com/2/users/{userId}"
            .WithTimeout(10)
            .WithHeader("Authorization", $"Bearer {_apiV2Token}")
            .GetStringAsync(ct);
        JObject body = JObject.Parse(resp).RemoveNullAndEmptyProperties();

        return ((string)body["data"]!["name"]!, (string)body["data"]!["username"]!);
    }
}
