using QQChannelFramework.Models.MessageModels;
using Rabbitool.Common.Util;
using Rabbitool.Model.DTO.Command;
using Rabbitool.Plugin.Command.Subscribe;
using Serilog;

namespace Rabbitool.Plugin.Command;

public static class CommandResponder
{
    private static readonly List<CommandInfo> _baseCommands = new()
    {
        new CommandInfo
        {
            Name = "/帮助",
            Format = new[] { "/帮助" },
            Example = "/帮助",
            Responder = RespondToHelpCommandAsync
        }
    };

    private static readonly List<CommandInfo> AllCommands = CommonUtil.CombineLists(
        _baseCommands,
        SubscribeCommandResponder.AllSubscribeCommands);

    public static async Task<string> GenerateReplyMsgAsync(Message msg, CancellationToken ct = default)
    {
        List<string> cmd = new();

        try
        {
            cmd = msg.Content.Replace("\xa0", " ").Split(" ", StringSplitOptions.RemoveEmptyEntries)[1..].ToList();

            CommandInfo? cmdInfo = AllCommands.Find(c => c.Name == cmd[0]);
            return cmdInfo == null
                ? "错误：指令错误！\n输入 /帮助 获取指令列表"
                : await cmdInfo.Responder(cmd, msg, ct);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to respond command!\nCommand: {command}", cmd);
            return "错误：处理指令时发生内部错误！";
        }
    }

    private static async Task<string> RespondToHelpCommandAsync(
        List<string> cmd, Message msg, CancellationToken ct)
    {
        string commands = "";
        foreach (CommandInfo v in AllCommands)
            commands += string.Join(" ", v.Format) + "\n";
        return $"支持的命令：\n{commands}\n详细设置请前往项目主页。";
    }
}