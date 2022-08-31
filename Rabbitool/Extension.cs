using System.Text.RegularExpressions;

namespace Rabbitool;

public static class Extension
{
    public static string AddRedirectToUrls(this string text, string redirect)
    {
        if (text is "")
        {
            text = "（无文本）";
        }
        else
        {
            text = Regex.Replace(
                text,
                @"(http|https):\/\/[\w\-_]+(\.[\w\-_]+)+([\w\-\.,@?^=%&:/~\+#]*[\w\-\@?^=%&/~\+#])?",
                (Match m) => redirect + m.Value);
        }

        return text;
    }
}
