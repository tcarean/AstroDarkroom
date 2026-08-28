namespace MindfulnessReminder.Core;

/// <summary>Abstracts the passage of time so debouncing can be tested without waiting.</summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

public sealed class SystemClock : IClock
{
    public static readonly SystemClock Instance = new();

    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
