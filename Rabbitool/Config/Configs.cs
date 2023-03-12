using System.ComponentModel.DataAnnotations;
using YamlDotNet.Serialization;

namespace Rabbitool.Config;

public class Configs
{
    public bool InTestEnvironment { get; set; } = false;

    [Required]
    public string RedirectUrl { get; set; } = null!;

    [Required]
    public string UserAgent { get; set; } = null!;

    [Required]
    public string DbPath { get; set; } = null!;

    public Proxy? Proxy { get; set; }

    [Required]
    public LogInfo Log { get; set; } = null!;

    [Required]
    public Interval Interval { get; set; } = null!;

    [Required]
    public Cos Cos { get; set; } = null!;

    [Required]
    [YamlMember(Alias = "qqbot")]
    public QQBot QQBot { get; set; } = null!;

    [Required]
    public Youtube Youtube { get; set; } = null!;

    public Notifier? Notifier { get; set; }
    public Twitter? Twitter { get; set; } = null!;

    public static Configs Load(string path)
    {
        string file = File.ReadAllText(path);
        IDeserializer deserializer = new DeserializerBuilder()
            .WithNamingConvention(PascalCaseToCamelCaseNamingConvention.Instance)
            .Build();
        return deserializer.Deserialize<Configs>(file);
    }
}

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
