namespace MindfulnessReminder.Core;

/// <summary>
/// Raises an event when the interactive session is unlocked. The production implementation
/// sits on SystemEvents.SessionSwitch; keeping it behind this interface means the whole
/// reminder policy is testable, and a WTSRegisterSessionNotification implementation could
/// be swapped in without touching anything else.
/// </summary>
public interface ISessionMonitor : IDisposable
{
    /// <summary>Raised on the UI thread after the workstation is unlocked.</summary>
    event EventHandler? Unlocked;

    void Start();
}
