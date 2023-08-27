using System.Text.RegularExpressions;
using Rabbitool.Conf;

namespace Rabbitool;

public static partial class Extension
{
    public static string AddRedirectToUrls(this string text)
    {
        if (text == "")
        {
            text = "（无文本）";
        }
        else
        {
            text = UrlRegex().Replace(text, m => Configs.R.RedirectUrl + m.Value);
            text = BLiveRegex().Replace(text, m => Configs.R.RedirectUrl + "https://" + m.Value);
        }

        return text;
    }

    [GeneratedRegex(@"(http|https):\/\/[\w\-_]+(\.[\w\-_]+)+([\w\-\.,@?^=%&:/~\+#]*[\w\-\@?^=%&/~\+#])?")]
    private static partial Regex UrlRegex();

    [GeneratedRegex(@"(?<!://)live\.bilibili\.com")]
    private static partial Regex BLiveRegex();
}