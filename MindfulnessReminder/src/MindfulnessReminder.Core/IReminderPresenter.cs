namespace MindfulnessReminder.Core;

/// <summary>The reminder window, as far as the coordinator needs to know about it.</summary>
public interface IReminderPresenter
{
    /// <summary>True while a reminder is on screen, or scheduled to appear shortly.</summary>
    bool IsShowing { get; }

    void Show();

    void BringToFront();
}
