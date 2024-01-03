using System.Text.RegularExpressions;

namespace Rabbitool;

public static partial class Extension
{
    public static string AddRedirectToUrls(this string text, string redirectUrl)
    {
        text = text == ""
            ? "（无文本）"
            : UrlRegex().Replace(text, m => redirectUrl + m.Value);

        return text;
    }

    [GeneratedRegex(
        @"((http|https)://)?[\w\-]+(\.[\w\-]+)?\.(com|cn|net|org|md|icu|top|xyz|jp|gov|edu|me|tv|la|cc|io|info|so|one|link|moe|pm)([\w\-.,@?^=%&:/~+#]*[\w\-@?^=%&/~+#])?")]
    private static partial Regex UrlRegex();
}