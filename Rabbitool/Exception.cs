using Newtonsoft.Json.Linq;

namespace Rabbitool;

public class QQBotApiException : Exception
{
    public QQBotApiException(string message) : base(message)
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
    public BilibiliApiException(string message, int code, JObject body)
        : base($"{message}\nCode: {code}\nBody: {body}")
    {
    }

    public BilibiliApiException(string message, int code, string body)
        : base($"{message}\nCode: {code}\nBody: {body}")
    {
    }
}

public class TwitterApiException : Exception
{
    public TwitterApiException(string message) : base(message)
    {
    }

    public TwitterApiException(string message, string screenName)
        : base($"{message}\nScreenName: {screenName}")
    {
    }
}

public class YoutubeApiException : Exception
{
    public YoutubeApiException(string message) : base(message)
    {
    }

    public YoutubeApiException(string message, string channelId)
        : base($"{message}\nChannelId: {channelId}")
    {
    }
}

public class UninitializedException : Exception
{
    public UninitializedException(string message) : base(message)
    {
    }
}
