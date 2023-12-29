using System.ComponentModel.DataAnnotations;
using Rabbitool.Common.Extension;
using YamlDotNet.Serialization;

namespace Rabbitool.Common.Configs;

public class Settings
{
    public static Settings R = null!;

    // common
    [Required] public required string RedirectUrl { get; init; }

    [Required] public required string UserAgent { get; init; }

    public bool InTestEnvironment { get; init; }

    [Required] public required string DbPath { get; init; }

    [Required] public required Logger DefaultLogger { get; init; }

    public Notifier? Notifier { get; init; }

    public Proxy? Proxy { get; init; }

    // services
    [Required] public required Cos Cos { get; init; }

    [Required]
    [YamlMember(Alias = "qqBot")]
    public required QQBot QQBot { get; init; }

    public Youtube? Youtube { get; init; }

    public Twitter? Twitter { get; init; }

    public static Settings Load(string path)
    {
        string file = File.ReadAllText(path);
        IDeserializer deserializer = new DeserializerBuilder()
            .WithNamingConvention(PascalCaseToCamelCaseNamingConvention.Instance)
            .WithRequiredPropertyValidation()
            .Build();
        R = deserializer.Deserialize<Settings>(file);
        return R;
    }
}

/// <summary>
///     首字母转小写
/// </summary>
public sealed class PascalCaseToCamelCaseNamingConvention : INamingConvention
{
    public static readonly INamingConvention Instance = new PascalCaseToCamelCaseNamingConvention();

    private PascalCaseToCamelCaseNamingConvention()
    {
    }

    public string Apply(string value)
    {
        char[] a = value.ToCharArray();
        a[0] = char.ToLower(a[0]);
        return new string(a);
    }
}