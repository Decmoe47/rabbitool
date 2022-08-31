using QQChannelFramework.Models.MessageModels;
using Rabbitool.Model.DTO.Command;
using Rabbitool.Plugin.Command.Subscribe;
using Serilog;

namespace Rabbitool.Plugin.Command;

public static class CommandResponder
{
    private static readonly List<CommandInfo> _commands = SubscribeCommandResponder.AllSubscribeCommands;

    private static readonly List<CommandInfo> _baseCommands = new()
    {
        new CommandInfo
        {
            Name = "/帮助",
            Format = new string[1]{ "/帮助" },
            Example = "/帮助",
            Responder = RespondToHelpCommandAsync
        }
    };

    private static readonly List<CommandInfo> _allCommands = _baseCommands.Concat(_commands).ToList();

    public static async Task<string> GenerateReplyMsgAsync(Message msg, CancellationToken cancellationToken = default)
    {
        List<string> clearCommand = new();

        try
        {
            string[] temp = msg.Content.Replace("\xa0", " ").Split(" ")[1..];
            foreach (string v in temp)
            {
                if (v != "")
                    clearCommand.Add(v);
            }

            bool isValidCommand = false;
            foreach (CommandInfo c in _allCommands)
            {
                if (c.Name == clearCommand[0])
                {
                    isValidCommand = true;
                    break;
                }
            }
            if (!isValidCommand)
                return "错误：指令错误！\n输入 /帮助 获取指令列表";

            CommandInfo? cmdInfo = _allCommands.Find(c => c.Name == clearCommand[0]);

            if (cmdInfo is null)
            {
                Log.Error("Couldn't get the responder for the command {command}!", clearCommand);
                return "错误：处理指令时发生内部错误！";
            }
            return await cmdInfo.Responder(clearCommand, msg, cancellationToken);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to respond command!\nCommand: {command}", clearCommand);
            return "错误：处理指令时发生内部错误！";
        }
    }

#pragma warning disable CS1998 // 异步方法缺少 "await" 运算符，将以同步方式运行

    private static async Task<string> RespondToHelpCommandAsync(
        List<string> command, Message message, CancellationToken cancellationToken)
    {
        string commands = "";
        foreach (CommandInfo v in _commands)
            commands += string.Join(" ", v.Format) + "\n";
        return $"支持的命令：\n{commands}\n详细设置请前往项目主页。";
    }

#pragma warning restore CS1998 // 异步方法缺少 "await" 运算符，将以同步方式运行
}
