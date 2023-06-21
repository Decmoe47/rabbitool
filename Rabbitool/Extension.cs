using System.Text.RegularExpressions;
using Rabbitool.Conf;

namespace Rabbitool;

public static class Extension
{
    public static string AddRedirectToUrls(this string text)
    {
        if (text == "")
        {
            text = "（无文本）";
        }
        else
        {
            text = Regex.Replace(
                text,
                @"(http|https):\/\/[\w\-_]+(\.[\w\-_]+)+([\w\-\.,@?^=%&:/~\+#]*[\w\-\@?^=%&/~\+#])?",
                (Match m) => Configs.R.RedirectUrl + m.Value);
        }

        return text;
    }
}