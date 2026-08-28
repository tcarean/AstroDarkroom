namespace MindfulnessReminder.Core;

/// <summary>A screen work area, in device-independent units.</summary>
public readonly record struct WorkArea(double Left, double Top, double Width, double Height);

/// <summary>
/// Where the reminder should sit.
///
/// WPF's CenterScreen picks whichever monitor Windows happened to place the new handle on,
/// which on a multi-monitor desk is effectively random. This centres the window explicitly
/// on a chosen work area instead, and keeps it on screen if the area is unusually small.
/// </summary>
public static class WindowPlacement
{
    public static (double Left, double Top) CenterIn(WorkArea area, double width, double height)
    {
        var left = area.Left + (area.Width - width) / 2;
        var top = area.Top + (area.Height - height) / 2;

        // A window wider or taller than the work area would be centred with its top-left off
        // screen, putting the Close button out of reach. Pin it to the corner instead.
        return (Math.Max(left, area.Left), Math.Max(top, area.Top));
    }
}
