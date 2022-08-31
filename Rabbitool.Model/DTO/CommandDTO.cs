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
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public class SubscribeConfigType : Dictionary<string, dynamic>
{
}

public class CommandInfo
{
    public string Name { get; set; } = string.Empty;
    public string[] Format { get; set; } = null!;
    public string Example { get; set; } = string.Empty;
    public Func<List<string>, string, CancellationToken, Task<string>> Responder { get; set; } = null!;
}
