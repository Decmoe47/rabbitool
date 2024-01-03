namespace Rabbitool.Common.Constant;

public static class Constants
{
#if DEBUG
    public const string ConfigFilename = "appsettings.json";
#else
    public const string ConfigFilename = "appsettings.Development.json";
#endif
}