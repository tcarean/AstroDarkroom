namespace MindfulnessReminder.Core;

public enum AutoStartStatus
{
    /// <summary>No Run entry exists: the app will not start with Windows.</summary>
    Disabled,

    /// <summary>A Run entry exists and Windows will honour it.</summary>
    Enabled,

    /// <summary>
    /// A Run entry exists but the user switched it off from Task Manager's Startup tab,
    /// so Windows ignores it. The tray checkbox must show this as "off".
    /// </summary>
    BlockedByWindows,
}
