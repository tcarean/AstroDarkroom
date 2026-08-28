namespace MindfulnessReminder.Core;

/// <summary>
/// The slice of the per-user registry this application needs. Keeping it behind an
/// interface is what lets the auto-start logic be unit tested off a real machine.
/// All key paths are relative to HKEY_CURRENT_USER.
/// </summary>
public interface IRegistryStore
{
    string? GetString(string keyPath, string valueName);

    byte[]? GetBinary(string keyPath, string valueName);

    void SetString(string keyPath, string valueName, string value);

    /// <summary>Removes the value if present; does nothing when the key or value is missing.</summary>
    void DeleteValue(string keyPath, string valueName);
}
