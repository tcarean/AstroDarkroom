namespace MindfulnessReminder.Core;

/// <summary>
/// Turns unlock events into at most one reminder window.
///
/// Two things make this less trivial than it sounds. Windows can raise SessionUnlock twice
/// for a single unlock, so bursts are collapsed by a short debounce window. And if a
/// reminder from a previous unlock is still on screen, a second window would be noise --
/// the existing one is raised instead.
/// </summary>
public sealed class ReminderCoordinator : IDisposable
{
    public static readonly TimeSpan DefaultDebounce = TimeSpan.FromSeconds(2);

    private readonly ISessionMonitor _monitor;
    private readonly IReminderPresenter _presenter;
    private readonly IClock _clock;
    private readonly TimeSpan _debounce;

    private DateTimeOffset? _lastShownAt;
    private bool _started;
    private bool _disposed;

    public ReminderCoordinator(
        ISessionMonitor monitor,
        IReminderPresenter presenter,
        IClock? clock = null,
        TimeSpan? debounce = null)
    {
        ArgumentNullException.ThrowIfNull(monitor);
        ArgumentNullException.ThrowIfNull(presenter);

        _monitor = monitor;
        _presenter = presenter;
        _clock = clock ?? SystemClock.Instance;
        _debounce = debounce ?? DefaultDebounce;
    }

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_started)
        {
            return;
        }

        _monitor.Unlocked += OnUnlocked;
        _monitor.Start();
        _started = true;
    }

    private void OnUnlocked(object? sender, EventArgs e) => HandleUnlock();

    internal void HandleUnlock()
    {
        if (_presenter.IsShowing)
        {
            _presenter.BringToFront();
            return;
        }

        var now = _clock.UtcNow;
        if (_lastShownAt is { } last && now - last < _debounce)
        {
            return;
        }

        _lastShownAt = now;
        _presenter.Show();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_started)
        {
            _monitor.Unlocked -= OnUnlocked;
        }

        _monitor.Dispose();
        _disposed = true;
    }
}
