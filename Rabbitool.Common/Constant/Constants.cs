namespace Rabbitool.Common.Constant;

public static class Constants
{
#if DEBUG
    public const string ConfigFilename = "appsettings.Development.json";
#else
    public const string ConfigFilename = "appsettings.json";
#endif

    public static readonly int[] BilibiliAntiCrawlerErrorCodes = [-401, -509, -799, -352];
}