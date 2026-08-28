using System.Windows;
using System.Windows.Threading;
using MindfulnessReminder.Core;

namespace MindfulnessReminder.App;

/// <summary>
/// Owns the single <see cref="BreakWindow"/> instance.
///
/// The window is not shown the instant the unlock event arrives: the shell is still
/// restoring the desktop then, and a window created into that moment often ends up behind
/// everything else. A short delay costs nothing and makes the reminder land on top.
/// </summary>
internal sealed class WpfReminderPresenter : IReminderPresenter
{
    public static readonly TimeSpan DefaultShowDelay = TimeSpan.FromMilliseconds(600);

    private readonly Dispatcher _dispatcher;
    private readonly TimeSpan _showDelay;

    private BreakWindow? _window;
    private DispatcherTimer? _pendingShow;

    public WpfReminderPresenter(Dispatcher dispatcher, TimeSpan? showDelay = null)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);

        _dispatcher = dispatcher;
        _showDelay = showDelay ?? DefaultShowDelay;
    }

    /// <summary>A queued show counts as showing, so a burst cannot queue two windows.</summary>
    public bool IsShowing => _window is not null || _pendingShow is not null;

    public void Show()
    {
        if (IsShowing)
        {
            BringToFront();
            return;
        }

        _pendingShow = new DispatcherTimer(DispatcherPriority.Normal, _dispatcher)
        {
            Interval = _showDelay,
        };

        _pendingShow.Tick += OnPendingShowElapsed;
        _pendingShow.Start();
    }

    private void OnPendingShowElapsed(object? sender, EventArgs e)
    {
        CancelPendingShow();

        var window = new BreakWindow();
        window.Closed += OnWindowClosed;
        _window = window;
        window.Show();
    }

    public void BringToFront()
    {
        if (_window is not { } window)
        {
            return;
        }

        if (window.WindowState == WindowState.Minimized)
        {
            window.WindowState = WindowState.Normal;
        }

        window.Topmost = true;
        window.Activate();
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        if (sender is BreakWindow window)
        {
            window.Closed -= OnWindowClosed;
        }

        _window = null;
    }

    private void CancelPendingShow()
    {
        if (_pendingShow is not { } timer)
        {
            return;
        }

        timer.Stop();
        timer.Tick -= OnPendingShowElapsed;
        _pendingShow = null;
    }
}
