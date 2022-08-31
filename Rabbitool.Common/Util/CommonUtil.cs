using System.Reflection;
using System.Text.RegularExpressions;

namespace Rabbitool.Common.Util;

public class CommonUtil
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
}
