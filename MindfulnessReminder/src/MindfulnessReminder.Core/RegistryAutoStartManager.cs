namespace MindfulnessReminder.Core;

/// <summary>
/// Auto-start backed by HKCU\...\CurrentVersion\Run. No elevation is needed, and the
/// entry is one value that can be added or removed at will.
///
/// The Run key alone is not the whole truth: Task Manager's Startup tab does not delete
/// the value, it records a veto in Explorer\StartupApproved\Run. A checkbox that only
/// looked at the Run key would happily report "on" for an entry Windows is ignoring, so
/// <see cref="GetStatus"/> reads both.
/// </summary>
public sealed class RegistryAutoStartManager : IAutoStartManager
{
    public const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public const string StartupApprovedKeyPath =
        @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";

    private readonly IRegistryStore _registry;
    private readonly string _valueName;
    private readonly string _executablePath;

    public RegistryAutoStartManager(IRegistryStore registry, string valueName, string executablePath)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentException.ThrowIfNullOrWhiteSpace(valueName);
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);

        _registry = registry;
        _valueName = valueName;
        _executablePath = executablePath;
    }

    /// <summary>The exact string this manager writes to the Run key.</summary>
    public string Command => Quote(_executablePath);

    public AutoStartStatus GetStatus()
    {
        var command = _registry.GetString(RunKeyPath, _valueName);
        if (string.IsNullOrWhiteSpace(command))
        {
            return AutoStartStatus.Disabled;
        }

        return IsApprovalRevoked() ? AutoStartStatus.BlockedByWindows : AutoStartStatus.Enabled;
    }

    public void Enable()
    {
        _registry.SetString(RunKeyPath, _valueName, Command);

        // Turning the checkbox back on has to clear a Task Manager veto too, otherwise the
        // entry is written and still ignored. Deleting the approval value is what Windows
        // treats as "never vetoed".
        _registry.DeleteValue(StartupApprovedKeyPath, _valueName);
    }

    public void Disable() => _registry.DeleteValue(RunKeyPath, _valueName);

    public bool RepairPathIfNeeded()
    {
        var command = _registry.GetString(RunKeyPath, _valueName);
        if (string.IsNullOrWhiteSpace(command))
        {
            return false;
        }

        if (string.Equals(command, Command, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        _registry.SetString(RunKeyPath, _valueName, Command);
        return true;
    }

    /// <summary>
    /// The approval value is a 12-byte blob whose first byte carries the flag: an even
    /// value means enabled (0x02), an odd one means the user disabled it (0x03).
    /// A missing value means the entry was never vetoed.
    /// </summary>
    private bool IsApprovalRevoked()
    {
        var approval = _registry.GetBinary(StartupApprovedKeyPath, _valueName);
        return approval is { Length: > 0 } && (approval[0] & 0x01) != 0;
    }

    private static string Quote(string path) => $"\"{path}\"";
}
