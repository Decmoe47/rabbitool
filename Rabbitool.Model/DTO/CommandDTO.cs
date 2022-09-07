using System.Diagnostics.CodeAnalysis;
using QQChannelFramework.Models.MessageModels;

namespace Rabbitool.Model.DTO.Command;

public class SubscribeCommandDTO
{
    public string Command { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public string? SubscribeId { get; set; }
    public SubscribeCommandQQChannelDTO QQChannel { get; set; } = new SubscribeCommandQQChannelDTO();
    public SubscribeConfigType? Configs { get; set; }
}

public class SubscribeCommandQQChannelDTO
{
    public string GuildId { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public class SubscribeConfigType : Dictionary<string, dynamic>
{
    public bool TryGetValue<T>(string key, [MaybeNullWhen(false)] out T value)
    {
        bool result = base.TryGetValue(key, out dynamic? v);
        value = result && v != null ? v : default(T);
        return result;
    }
}

public class CommandInfo
{
    public string Name { get; set; } = string.Empty;
    public string[] Format { get; set; } = null!;
    public string Example { get; set; } = string.Empty;
    public Func<List<string>, Message, CancellationToken, Task<string>> Responder { get; set; } = null!;
}
