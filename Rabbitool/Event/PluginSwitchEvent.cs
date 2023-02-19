namespace Rabbitool.Event;

public class PluginSwitchEvent
{
    public delegate void SwitchBilibiliPluginDelegate(bool status);

    public delegate void SwitchYoutubePluginDelegate(bool status);

    public delegate void SwitchTwitterPluginDelegate(bool status);

    public delegate void SwitchMailPluginDelegate(bool status);

    public delegate Dictionary<string, bool> GetAllPluginStatusDelegate();

    public static event SwitchBilibiliPluginDelegate? SwitchBilibiliPluginEvent;

    public static event SwitchYoutubePluginDelegate? SwitchYoutubePluginEvent;

    public static event SwitchTwitterPluginDelegate? SwitchTwitterPluginEvent;

    public static event SwitchMailPluginDelegate? SwitchMailPluginEvent;

    public static event GetAllPluginStatusDelegate? GetAllPluginStatusEvent;

    public static Dictionary<string, bool>? GetAllPluginsStatus()
    {
        return GetAllPluginStatusEvent is not null ? GetAllPluginStatusEvent() : null;
    }

    public static void OnBilibiliPluginSwitching(bool status)
    {
        if (SwitchBilibiliPluginEvent is not null)
            SwitchBilibiliPluginEvent(status);
    }

    public static void OnYoutubePluginSwitching(bool status)
    {
        if (SwitchYoutubePluginEvent is not null)
            SwitchYoutubePluginEvent(status);
    }

    public static void OnTwitterPluginSwitching(bool status)
    {
        if (SwitchTwitterPluginEvent is not null)
            SwitchTwitterPluginEvent(status);
    }

    public static void OnMailPluginSwitching(bool status)
    {
        if (SwitchMailPluginEvent is not null)
            SwitchMailPluginEvent(status);
    }
}
