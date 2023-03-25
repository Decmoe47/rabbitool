using System.Text.RegularExpressions;

namespace Rabbitool.Plugin;

public class PluginHelper
{
    public static string AddRedirectToUrls(string text, string redirect)
    {
        if (text == "")
            return "（无文本）";
        return Regex.Replace(
            text,
            @"(http|https):\/\/[\w\-_]+(\.[\w\-_]+)+([\w\-\.,@?^=%&:/~\+#]*[\w\-\@?^=%&/~\+#])?",
            (Match m) => { return redirect + m.Value; });
    }
}
