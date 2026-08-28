namespace MindfulnessReminder.Core;

public interface IAutoStartManager
{
    AutoStartStatus GetStatus();

    void Enable();

    void Disable();

    /// <summary>
    /// Rewrites the registered command when the executable has moved since it was registered.
    /// Returns <c>true</c> when a repair was made. Never lifts a Task Manager block.
    /// </summary>
    bool RepairPathIfNeeded();
}
