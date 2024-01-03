using Autofac.Annotation;
using MyBot.Models.MessageModels;
using Rabbitool.Model.DTO.Command;
using Rabbitool.Plugin.Command.Subscribe;
using Serilog;

namespace Rabbitool.Plugin.Command;

[Component]
public class Commands(SubscribeCommands subscribeCommands)
{
    public async Task<string> GenerateReplyMsgAsync(Message msg, CancellationToken ct = default)
    {
        List<string> cmd = [];

        try
        {
            cmd = msg.Content.Replace("\xa0", " ").Split(" ", StringSplitOptions.RemoveEmptyEntries)[1..].ToList();
            CommandInfo? cmdInfo = GetAllCommands().Find(c => c.Name == cmd[0]);
            return cmdInfo == null
                ? "错误：指令错误！\n输入 /帮助 获取指令列表"
                : await cmdInfo.Responder(cmd, msg);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to respond command!\nCommand: {command}", cmd);
            return "错误：处理指令时发生内部错误！";
        }
    }

    private List<CommandInfo> GetAllCommands()
    {
        return
        [
            new CommandInfo
            {
                Name = "/帮助",
                Format = ["/帮助"],
                Example = "/帮助",
                Responder = RespondToHelpCommandAsync
            },
            .. subscribeCommands.GetAllCommands()
        ];
    }

    private Task<string> RespondToHelpCommandAsync(List<string> cmd, Message msg)
    {
        string commands = GetAllCommands().Aggregate("", (current, v) => current + string.Join(" ", v.Format) + "\n");
        return Task.FromResult($"支持的命令：\n{commands}\n详细设置请前往项目主页。");
    }
}