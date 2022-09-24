namespace Rabbitool.Event;

public class PluginSwitchEvent
{
    public delegate void SwitchBilibiliPluginDelegate(bool status);

    public delegate void SwitchYoutubePluginDelegate(bool status);

    public delegate void SwitchTwitterPluginDelegate(bool status);

    public delegate void SwitchMailPluginDelegate(bool status);

    public delegate Dictionary<string, bool> GetPluginSwitchesDelegate();

    public static event SwitchBilibiliPluginDelegate? SwitchBilibiliPluginEvent;

    public static event SwitchYoutubePluginDelegate? SwitchYoutubePluginEvent;

    public static event SwitchTwitterPluginDelegate? SwitchTwitterPluginEvent;

    public static event SwitchMailPluginDelegate? SwitchMailPluginEvent;

    public static event GetPluginSwitchesDelegate? GetPluginSwitchesEvent;

    public static Dictionary<string, bool>? GetPluginsSwitches()
    {
        return GetPluginSwitchesEvent is not null ? GetPluginSwitchesEvent() : null;
    }

    public static void OnBilibiliPluginSwitched(bool status)
    {
        if (SwitchBilibiliPluginEvent is not null)
            SwitchBilibiliPluginEvent(status);
    }

    public static void OnYoutubePluginSwitched(bool status)
    {
        if (SwitchYoutubePluginEvent is not null)
            SwitchYoutubePluginEvent(status);
    }

    public static void OnTwitterPluginSwitched(bool status)
    {
        if (SwitchTwitterPluginEvent is not null)
            SwitchTwitterPluginEvent(status);
    }

    public static void OnMailPluginSwitched(bool status)
    {
        if (SwitchMailPluginEvent is not null)
            SwitchMailPluginEvent(status);
    }
}
