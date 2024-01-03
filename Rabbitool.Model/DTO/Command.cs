using System.Diagnostics.CodeAnalysis;
using MyBot.Models.MessageModels;

namespace Rabbitool.Model.DTO.Command;

public class SubscribeCommand
{
    public required string Command { get; set; }
    public required string Platform { get; set; }
    public string? SubscribeId { get; set; }
    public required SubscribeCommandQQChannel QQChannel { get; set; }
    public SubscribeConfigType? Configs { get; set; }
}

public class SubscribeCommandQQChannel
{
    public required string GuildId { get; set; }
    public required string GuildName { get; set; }
    public required string Id { get; set; }
    public required string Name { get; set; }
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
    public required string Name { get; set; }
    public required string[] Format { get; set; }
    public required string Example { get; set; }
    public required Func<List<string>, Message, Task<string>> Responder { get; set; }
}