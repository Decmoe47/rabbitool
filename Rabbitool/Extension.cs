﻿using System.Text.RegularExpressions;
using Rabbitool.Conf;

namespace Rabbitool;

public static partial class Extension
{
    public static string AddRedirectToUrls(this string text)
    {
        text = text == "" 
            ? "（无文本）" 
            : UrlRegex().Replace(text, m => Configs.R.RedirectUrl + m.Value);

        return text;
    }

    [GeneratedRegex(@"((http|https)://){0,1}[\w\-_]+(\.[\w\-_]+)+([\w\-.,@?^=%&:/~+#]*[\w\-@?^=%&/~+#])?")]
    private static partial Regex UrlRegex();
}