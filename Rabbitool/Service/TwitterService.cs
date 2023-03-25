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
    private readonly string _token;
    private readonly LimiterUtil _tweetApiLimiter = LimiterCollection.TwitterTweetApiLimiter;
    private readonly LimiterUtil _userApiLimiter = LimiterCollection.TwitterUserApiLimiter;

    public TwitterService(string token)
    {
        _token = token;
    }

    public async Task<Tweet> GetLatestTweetAsync(string screenName, CancellationToken ct = default)
    {
        (string userId, _) = await GetUserIdAsync(screenName, ct);

        _tweetApiLimiter.Wait(ct: ct);
        string resp = await $"https://api.twitter.com/2/users/{userId}/tweets"
            .WithTimeout(10)
            .WithOAuthBearerToken(_token)
            .SetQueryParams(new Dictionary<string, string>()
            {
                { "exclude", "retweets,replies" },
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
            (text, imgUrls, hasVideo) = await GetMediaiAsync(body, media, text, id, ct);

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

            origin = await GetOriginTweetAsync(body, originId, ct);
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

    private async Task<(string text, List<string>? imgUrls, bool hasVideo)> GetMediaiAsync(
        JObject body, JArray media, string text, string tweetId, CancellationToken ct)
    {
        bool hasVideo = false;
        List<string> imgUrls = new();

        foreach (JToken medium in media)
        {
            if ((string?)medium["expanded_url"] is string expandUrl)
                text = text.Replace((string)medium["url"]!, expandUrl);

            string? mediaKey = (string?)medium["media_key"];
            if (mediaKey == null)
                continue;

            try
            {
                if (mediaKey?.StartsWith("3_") == true)
                {
                    imgUrls.Add(await GetImageOrVideoThumbnailUrlAsync(body, mediaKey, tweetId, ct));
                    text = text.Replace((string)medium["expanded_url"]!, "");
                }
                else if (mediaKey?.StartsWith("7_") == true)
                {
                    hasVideo = true;
                    imgUrls.Add(await GetImageOrVideoThumbnailUrlAsync(body, mediaKey, tweetId, ct));
                    text = text.Replace((string)medium["expanded_url"]!, "");
                }
                else if (mediaKey?.StartsWith("13_") == true)    // 13应该是广告性质的视频
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
            .WithOAuthBearerToken(_token)
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

    private async Task<Tweet> GetOriginTweetAsync(
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
                    (text, imgUrls, hasVideo) = await GetMediaiAsync(body, media, text, originId, ct);

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

    private async Task<(string userId, string name)> GetUserIdAsync(string screenName, CancellationToken ct = default)
    {
        _userApiLimiter.Wait(ct: ct);
        string resp = await $"https://api.twitter.com/2/users/by/username/{screenName}"
            .WithTimeout(10)
            .WithHeader("Authorization", $"Bearer {_token}")
            .GetStringAsync(ct);
        JObject body = JObject.Parse(resp).RemoveNullAndEmptyProperties();

        return ((string)body["data"]!["id"]!, (string)body["data"]!["name"]!);
    }

    private async Task<(string name, string screenName)> GetUserNameAsync(string userId, CancellationToken ct = default)
    {
        _userApiLimiter.Wait(ct: ct);
        string resp = await $"https://api.twitter.com/2/users/{userId}"
            .WithTimeout(10)
            .WithHeader("Authorization", $"Bearer {_token}")
            .GetStringAsync(ct);
        JObject body = JObject.Parse(resp).RemoveNullAndEmptyProperties();

        return ((string)body["data"]!["name"]!, (string)body["data"]!["username"]!);
    }
}
