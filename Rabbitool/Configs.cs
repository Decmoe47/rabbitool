using System.ComponentModel.DataAnnotations;
using YamlDotNet.Serialization;

namespace Rabbitool;

public class Configs
{
    public bool InTestEnvironment { get; set; } = false;
    public string? HttpProxy { get; set; }
    public string? HttpsProxy { get; set; }

    [Required]
    public string RedirectUrl { get; set; } = null!;

    [Required]
    public string UserAgent { get; set; } = null!;

    [Required]
    public string ConsoleLevel { get; set; } = null!;

    [Required]
    public string FileLevel { get; set; } = null!;

    [Required]
    public string DbPath { get; set; } = null!;

    public string? TestDbPath { get; set; }

    [Required]
    public Interval Interval { get; set; } = null!;

    [Required]
    public Cos Cos { get; set; } = null!;

    [Required]
    [YamlMember(Alias = "qqbot")]
    public QQBot QQBot { get; set; } = null!;

    public ErrorNotifier? ErrorNotifier { get; set; }
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

public class Interval
{
    [Required]
    public int BilibiliPlugin { get; set; }

    [Required]
    public int YoutubePlugin { get; set; }

    [Required]
    public int TwitterPlugin { get; set; }

    [Required]
    public int MailPlugin { get; set; }
}

public class ErrorNotifier
{
    [Required]
    public string SenderHost { get; set; } = null!;

    [Required]
    public int SenderPort { get; set; }

    [Required]
    public bool UsingSsl { get; set; }

    [Required]
    public string SenderUsername { get; set; } = null!;

    [Required]
    public string SenderPassword { get; set; } = null!;

    [Required]
    public string SenderAddress { get; set; } = null!;

    [Required]
    public string[] ReceiverAddresses { get; set; } = null!;

    [Required]
    public int RefreshMinutes { get; set; }

    [Required]
    public int MaxAmount { get; set; }

    [Required]
    public int AllowedAmount { get; set; }

    public Rabbitool.Common.Tool.ErrorNotifierOptions ToOptions()
    {
        return new Common.Tool.ErrorNotifierOptions()
        {
            Host = SenderHost,
            Port = SenderPort,
            Ssl = UsingSsl,
            Username = SenderUsername,
            Password = SenderPassword,
            From = SenderAddress,
            To = ReceiverAddresses,
            RefreshMinutes = RefreshMinutes,
            MaxAmount = MaxAmount,
            AllowedAmount = AllowedAmount
        };
    }
}

public class QQBot
{
    [Required]
    public string AppId { get; set; } = null!;

    [Required]
    public string Token { get; set; } = null!;

    [Required]
    [YamlMember(Alias = "isSandbox")]
    public bool IsSandBox { get; set; }
}

public class Cos
{
    [Required]
    public string SecretId { get; set; } = null!;

    [Required]
    public string SecretKey { get; set; } = null!;

    [Required]
    public string BucketName { get; set; } = null!;

    [Required]
    public string Region { get; set; } = null!;
}

public class Twitter
{
    [YamlMember(Alias = "x_csrf_token")]
    public string? XCsrfToken { get; set; }

    public string? Cookie { get; set; }
    public string? ApiV2Token { get; set; }
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
