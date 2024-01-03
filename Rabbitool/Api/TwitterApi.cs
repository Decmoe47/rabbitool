using System.Globalization;
using System.Threading.RateLimiting;
using Autofac.Annotation;
using Autofac.Annotation.Condition;
using Flurl.Http;
using Newtonsoft.Json.Linq;
using Rabbitool.Common.Configs;
using Rabbitool.Common.Exception;
using Rabbitool.Common.Extension;
using Rabbitool.Model.DTO.Twitter;
using Serilog;

namespace Rabbitool.Api;

[ConditionalOnProperty("twitter")]
[Component]
public class TwitterApi(TwitterConfig twitterConfig)
{
    private readonly RateLimiter _tweetApiLimiter = new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
    {
        QueueLimit = 1,
        ReplenishmentPeriod = TimeSpan.FromMinutes(1),
        TokenLimit = 11,
        TokensPerPeriod = 11
    }); // See https://developer.twitter.com/en/docs/twitter-api/tweets/timelines/migrate

    private readonly RateLimiter _userApiLimiter = new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
    {
        QueueLimit = 1,
        ReplenishmentPeriod = TimeSpan.FromMinutes(1),
        TokenLimit = 1,
        TokensPerPeriod = 1
    });

    public async Task<Tweet> GetLatestTweetAsync(string screenName, CancellationToken ct = default)
    {
        await _tweetApiLimiter.AcquireAsync(1, ct);

        string resp;
        if (twitterConfig is { XCsrfToken: not null, Cookie: not null })
            resp = await "https://api.twitter.com/1.1/statuses/user_timeline.json"
                .WithTimeout(10)
                .WithOAuthBearerToken(twitterConfig.BearerToken)
                .SetQueryParams(new Dictionary<string, string>
                {
                    { "count", "5" },
                    { "screen_name", screenName },
                    { "exclude_replies", "true" },
                    { "include_rts", "true" },
                    {
                        "tweet_mode", "extended"
                    } // https://stackoverflow.com/questions/38717816/twitter-api-text-field-value-is-truncated
                })
                .WithHeaders(new Dictionary<string, string>
                {
                    { "x-csrf-token", twitterConfig.XCsrfToken },
                    { "Cookie", twitterConfig.Cookie }
                })
                .GetStringAsync(cancellationToken: ct);
        else
            resp = await "https://api.twitter.com/1.1/statuses/user_timeline.json"
                .WithTimeout(10)
                .WithOAuthBearerToken(twitterConfig.BearerToken)
                .SetQueryParams(new Dictionary<string, string>
                {
                    { "count", "5" },
                    { "screen_name", screenName },
                    { "exclude_replies", "true" },
                    { "include_rts", "true" },
                    { "tweet_mode", "extended" }
                })
                .GetStringAsync(cancellationToken: ct);
        if (resp.Contains("errors"))
        {
            JObject errBody = JObject.Parse(resp).RemoveNullAndEmptyProperties();
            string error = (int)errBody["errors"]![0]!["code"]! + (string)errBody["errors"]![0]!["message"]!;
            throw new TwitterApiException(error);
        }

        JArray body = JArray.Parse(resp);

        JObject tweet = (JObject)body[0];
        Tweet result = Parse(tweet);

        Tweet? origin = null;
        if ((JObject?)tweet["retweeted_status"] is { } retweetedStatus)
        {
            result.Type = TweetTypeEnum.RT;
            origin = Parse(retweetedStatus);
        }
        else if ((bool?)tweet["is_quote_status"] is true)
        {
            result.Type = TweetTypeEnum.Quote;
            origin = Parse((JObject)tweet["quoted_status"]!);
        }

        if (origin != null)
        {
            result.Origin = origin;
            result.Text = result.Text.Replace(origin.Url, "");
        }

        return result;
    }

    private Tweet Parse(JObject tweet)
    {
        string id = (string)tweet["id"]!;
        string text = (string)tweet["full_text"]!;

        List<string>? imgUrls = null;
        string? videoUrl = null;
        if ((JArray?)tweet["extended_entities"]?["media"] is { } media)
            (text, imgUrls, videoUrl) = GetImageAndVideo(media, text, id);
        if ((JArray?)tweet["entities"]?["urls"] is { } urls)
            text = ReplaceUrlWithExpandedUrl(urls, text);

        string screenName = (string)tweet["user"]!["screen_name"]!;
        DateTime pubTime = DateTime
            .ParseExact(
                (string)tweet["created_at"]!,
                "ddd MMM dd HH:mm:ss zz00 yyyy",
                DateTimeFormatInfo.InvariantInfo,
                DateTimeStyles.AdjustToUniversal)
            .ToUniversalTime();
        return new Tweet
        {
            Id = id,
            Type = TweetTypeEnum.Common,
            Url = $"https://twitter.com/{screenName}/status/{id}",
            PubTime = pubTime,
            Author = (string)tweet["user"]!["name"]!,
            AuthorScreenName = screenName,
            Text = text,
            ImageUrls = imgUrls,
            VideoUrl = videoUrl
        };
    }

    private static string ReplaceUrlWithExpandedUrl(JArray urls, string text)
    {
        foreach (JToken url in urls)
            text = text.Replace((string)url["url"]!, (string)url["expanded_url"]!);
        return text;
    }

    private (string text, List<string>? imgUrls, string? videoUrl) GetImageAndVideo(
        JArray media, string text, string tweetId)
    {
        string? videoUrl = null;
        List<string> imgUrls = new();

        foreach (JToken medium in media)
            try
            {
                string mediaType = (string)medium["type"]!;
                if (mediaType != "photo" && mediaType != "video")
                    throw new UnsupportedException($"Unsupported media type {mediaType} from tweet id {tweetId}");
                if (mediaType == "video")
                {
                    JArray videoUrls = (JArray)medium["video_info"]!["variants"]!;
                    int maxBitrate = (int)videoUrls[0]["bitrate"]!;
                    int maxBitrateIndex = 0;
                    for (int i = 1; i < videoUrls.Count; i++)
                    {
                        int bitrate = (int?)videoUrls[i]["bitrate"] ?? 0;
                        if (bitrate > maxBitrate)
                        {
                            maxBitrate = bitrate;
                            maxBitrateIndex = i;
                        }
                    }

                    videoUrl = (string?)videoUrls[maxBitrateIndex]["url"];
                }

                // 获取原始尺寸大小
                imgUrls.Add(string.Join(".", ((string)medium["media_url_https"]!).Split('.')[..^1]) +
                            "?format=jpg&name=large");
                text = text.Replace((string)medium["url"]!, "");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to get image url!\nTweetId: {tweetId}", tweetId);
            }

        return (text, imgUrls.Count != 0 ? imgUrls : null, videoUrl);
    }
}