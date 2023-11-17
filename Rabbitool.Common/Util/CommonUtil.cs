using System.Reflection;
using System.Text.RegularExpressions;

namespace Rabbitool.Common.Util;

public static partial class CommonUtil
{
    /// <summary>
    ///     不存在的属性名会被忽略
    /// </summary>
    /// <exception cref="NullReferenceException"></exception>
    /// <exception cref="ArgumentException"></exception>
    /// <exception cref="TargetException"></exception>
    /// <exception cref="MethodAccessException"></exception>
    /// <exception cref="TargetInvocationException"></exception>
    public static void UpdateProperties(object cls, Dictionary<string, dynamic> props)
    {
        Type typeofCls = cls.GetType();
        foreach (KeyValuePair<string, dynamic> prop in props)
        {
            char[] a = prop.Key.ToCharArray();
            a[0] = char.ToUpper(a[0]);
            typeofCls.GetProperty(new string(a))?.SetValue(cls, prop.Value);
        }
    }

    public static bool ExistUrl(string text)
    {
        return MyRegex().IsMatch(text);
    }

    public static List<T> CombineLists<T>(
        List<T> lst1, List<T> lst2, List<T>? lst3 = null, List<T>? lst4 = null, List<T>? lst5 = null,
        List<T>? lst6 = null)
    {
        IEnumerable<T> lst = lst1.Concat(lst2);
        if (lst3 != null)
            lst = lst.Concat(lst3);
        if (lst4 != null)
            lst = lst.Concat(lst4);
        if (lst5 != null)
            lst = lst.Concat(lst5);
        if (lst6 != null)
            lst = lst.Concat(lst6);
        return lst.ToList();
    }

    [GeneratedRegex(@"((http|https)://)?[\w\-]+(\.[\w\-]+)?\.(com|cn|net|org|md|icu|top|xyz|jp|gov|edu|me|tv|la|cc|io|info|so|one|link|moe|pm)([\w\-.,@?^=%&:/~+#]*[\w\-@?^=%&/~+#])?")]
    private static partial Regex MyRegex();
}