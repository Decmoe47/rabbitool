using Newtonsoft.Json.Linq;

namespace Rabbitool;

public class QQBotApiException : Exception
{
    public QQBotApiException(string message) : base(message)
    {
    }

    public QQBotApiException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

public class CosFileUploadException : Exception
{
    public CosFileUploadException(string message) : base(message)
    {
    }
}

public class BilibiliApiException : Exception
{
    public readonly JObject Body;
    public readonly int Code;

    public BilibiliApiException(string message, int code, JObject body)
        : base($"{message}\nCode: {code}\nBody: {body}")
    {
        Code = code;
        Body = body;
    }

    public BilibiliApiException(string message, int code, string body)
        : base($"{message}\nCode: {code}\nBody: {body}")
    {
        Code = code;
        Body = JObject.Parse(body);
    }
}

public class TwitterApiException : Exception
{
    public readonly string? ScreenName;

    public TwitterApiException(string message) : base(message)
    {
    }

    public TwitterApiException(string message, string screenName)
        : base($"{message}\nScreenName: {screenName}")
    {
        ScreenName = screenName;
    }
}

public class YoutubeApiException : Exception
{
    public readonly string? ChannelId;

    public YoutubeApiException(string message) : base(message)
    {
    }

    public YoutubeApiException(string message, string channelId)
        : base($"{message}\nChannelId: {channelId}")
    {
        ChannelId = channelId;
    }
}