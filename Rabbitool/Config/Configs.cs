using System.ComponentModel.DataAnnotations;
using YamlDotNet.Serialization;

namespace Rabbitool.Conf;

public class Configs
{
    // env
    [Required]
    public required string RedirectUrl { get; set; }

    public bool InTestEnvironment { get; set; } = false;

    [Required]
    public required string DbPath { get; set; }

    [Required]
    public required Logger DefaultLogger { get; set; }

    public Notifier? Notifier { get; set; }

    public Proxy? Proxy { get; set; }

    // services
    [Required]
    public required Cos Cos { get; set; }

    [Required]
    [YamlMember(Alias = "qqBot")]
    public required QQBot QQBot { get; set; }

    public Youtube? Youtube { get; set; }

    public Twitter? Twitter { get; set; }

    public static Configs R = null!;

    public static Configs Load(string path)
    {
        string file = File.ReadAllText(path);
        IDeserializer deserializer = new DeserializerBuilder()
            .WithNamingConvention(PascalCaseToCamelCaseNamingConvention.Instance)
            .Build();
        R = deserializer.Deserialize<Configs>(file);
        return R;
    }
}

/// <summary>
/// 首字母转小写
/// </summary>
public sealed class PascalCaseToCamelCaseNamingConvention : INamingConvention
{
#pragma warning disable CS0618 // 类型或成员已过时
    public static readonly INamingConvention Instance = new PascalCaseToCamelCaseNamingConvention();
#pragma warning restore CS0618 // 类型或成员已过时

    [Obsolete("Use the Instance static field instead of creating new instances")]
    public PascalCaseToCamelCaseNamingConvention()
    { }

    public string Apply(string value)
    {
        char[] a = value.ToCharArray();
        a[0] = char.ToLower(a[0]);
        return new string(a);
    }
}