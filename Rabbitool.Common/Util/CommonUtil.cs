using System.Reflection;
using System.Text.RegularExpressions;

namespace Rabbitool.Common.Util;

public static class CommonUtil
{
    /// <summary>
    /// 不存在的属性名会被忽略
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
        return Regex.IsMatch(text, @"(http|https)://[\w\-_]+(\.[\w\-_]+)+([\w\-.,@?^=%&:/~+#]*[\w\-@?^=%&/~+#])?");
    }

    public static List<T> CombineLists<T>(List<T> lst1, List<T> lst2, List<T>? lst3 = null, List<T>? lst4 = null, List<T>? lst5 = null, List<T>? lst6 = null)
    {
        IEnumerable<T> lst = lst1.Concat(lst2);
        if (lst3 is not null)
            lst = lst.Concat(lst3);
        if (lst4 is not null)
            lst = lst.Concat(lst4);
        if (lst5 is not null)
            lst = lst.Concat(lst5);
        if (lst6 is not null)
            lst = lst.Concat(lst6);
        return lst.ToList();
    }
}
