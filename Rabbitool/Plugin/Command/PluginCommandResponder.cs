using QQChannelFramework.Models.MessageModels;
using Rabbitool.Event;

namespace Rabbitool.Plugin.Command;

public class PluginCommandResponder
{
#pragma warning disable CS1998 // 异步方法缺少 "await" 运算符，将以同步方式运行

    public static async Task<string> RespondToPluginCommandAsync(
#pragma warning restore CS1998 // 异步方法缺少 "await" 运算符，将以同步方式运行
        List<string> command, Message message, CancellationToken cancellationToken)
    {
        switch (command[1])
        {
            case "list" or "ls":
                Dictionary<string, bool>? switches = PluginSwitchEvent.GetPluginsSwitches();
                if (switches is null)
                    return "No plugin is running.";

                string result = "";
                foreach (KeyValuePair<string, bool> sw in switches)
                    result += sw.Key + ": " + ReplaceBoolToSwitchStr(sw.Value) + "\n";

                return result;

            case "start":
                return StartPlugin(command[2]);

            case "stop":
                return StopPlugin(command[2]);

            default:
                return "[Error] Invalid command!";
        }
    }

    private static string StartPlugin(string pluginName)
    {
        switch (pluginName)
        {
            case "bilibili":
                PluginSwitchEvent.OnBilibiliPluginSwitched(true);
                return "Bilibili plugin started successfully!";

            case "youtube":
                PluginSwitchEvent.OnYoutubePluginSwitched(true);
                return "Youtube plugin started successfully!";

            case "twitter":
                PluginSwitchEvent.OnTwitterPluginSwitched(true);
                return "Twitter plugin started successfully!";

            case "Mail":
                PluginSwitchEvent.OnMailPluginSwitched(true);
                return "Mail plugin started successfully!";

            default:
                return "[Error] Invalid plugin name!";
        }
    }

    private static string StopPlugin(string pluginName)
    {
        switch (pluginName)
        {
            case "bilibili":
                PluginSwitchEvent.OnBilibiliPluginSwitched(false);
                return "Bilibili plugin stopped successfully!";

            case "youtube":
                PluginSwitchEvent.OnYoutubePluginSwitched(false);
                return "Youtube plugin stopped successfully!";

            case "twitter":
                PluginSwitchEvent.OnTwitterPluginSwitched(false);
                return "Twitter plugin stopped successfully!";

            case "Mail":
                PluginSwitchEvent.OnMailPluginSwitched(false);
                return "Mail plugin stopped successfully!";

            default:
                return "[Error] Invalid plugin name!";
        }
    }

    private static string ReplaceBoolToSwitchStr(bool value)
    {
        return value ? "running" : "stopped";
    }
}
