using System.Drawing;
using System.Windows.Forms;
using MindfulnessReminder.Core;
using WpfApplication = System.Windows.Application;

namespace MindfulnessReminder.App;

/// <summary>
/// The tray icon and its menu: a checkbox that mirrors the auto-start state, and Exit.
///
/// The checked state is refreshed every time the menu opens rather than cached, because the
/// user can change auto-start behind our back from Task Manager's Startup tab.
/// </summary>
internal sealed class TrayIconHost : IDisposable
{
    private const string IconResourceUri = "pack://application:,,,/Assets/app.ico";

    private readonly IAutoStartManager _autoStart;
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _autoStartItem;
    private readonly Icon _icon;

    private bool _disposed;

    public TrayIconHost(IAutoStartManager autoStart)
    {
        ArgumentNullException.ThrowIfNull(autoStart);
        _autoStart = autoStart;

        _autoStartItem = new ToolStripMenuItem("Run at Windows startup")
        {
            CheckOnClick = false,
        };
        _autoStartItem.Click += OnAutoStartClick;

        var exitItem = new ToolStripMenuItem("Exit");
        exitItem.Click += (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty);

        var menu = new ContextMenuStrip();
        menu.Items.Add(_autoStartItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(exitItem);
        menu.Opening += (_, _) => RefreshAutoStartItem();

        _icon = LoadTrayIcon();

        _notifyIcon = new NotifyIcon
        {
            Icon = _icon,
            Text = "Mindfulness Reminder",
            ContextMenuStrip = menu,
            Visible = true,
        };

        RefreshAutoStartItem();
    }

    public event EventHandler? ExitRequested;

    public void ShowBalloon(string title, string text)
    {
        _notifyIcon.BalloonTipTitle = title;
        _notifyIcon.BalloonTipText = text;
        _notifyIcon.BalloonTipIcon = ToolTipIcon.Info;
        _notifyIcon.ShowBalloonTip(5000);
    }

    private void OnAutoStartClick(object? sender, EventArgs e)
    {
        try
        {
            // Anything other than a working entry -- absent, or vetoed in Task Manager --
            // turns into "enable", so one click always restores the intended state.
            if (_autoStart.GetStatus() == AutoStartStatus.Enabled)
            {
                _autoStart.Disable();
            }
            else
            {
                _autoStart.Enable();
            }
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException)
        {
            ShowBalloon(
                "Mindfulness Reminder",
                "Windows refused the startup setting change. Group policy may be managing it.");
        }

        RefreshAutoStartItem();
    }

    private void RefreshAutoStartItem()
    {
        var status = _autoStart.GetStatus();

        _autoStartItem.Checked = status == AutoStartStatus.Enabled;
        _autoStartItem.Text = status == AutoStartStatus.BlockedByWindows
            ? "Run at Windows startup (disabled in Task Manager)"
            : "Run at Windows startup";
    }

    private static Icon LoadTrayIcon()
    {
        var resource = WpfApplication.GetResourceStream(new Uri(IconResourceUri))
            ?? throw new InvalidOperationException($"Missing embedded resource: {IconResourceUri}");

        using var stream = resource.Stream;

        // Asking for the shell's small icon size picks the matching frame out of the
        // multi-resolution .ico, which keeps the tray crisp at every DPI.
        return new Icon(stream, SystemInformation.SmallIconSize);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        // Hiding first: a NotifyIcon disposed while visible can leave a ghost icon in the
        // tray until the user hovers over it.
        _notifyIcon.Visible = false;
        _notifyIcon.ContextMenuStrip?.Dispose();
        _notifyIcon.Dispose();
        _icon.Dispose();

        _disposed = true;
    }
}
