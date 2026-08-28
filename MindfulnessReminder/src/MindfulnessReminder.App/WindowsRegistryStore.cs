using Microsoft.Win32;
using MindfulnessReminder.Core;

namespace MindfulnessReminder.App;

/// <summary>The real HKEY_CURRENT_USER behind <see cref="IRegistryStore"/>.</summary>
internal sealed class WindowsRegistryStore : IRegistryStore
{
    public string? GetString(string keyPath, string valueName)
    {
        using var key = Registry.CurrentUser.OpenSubKey(keyPath);
        return key?.GetValue(valueName) as string;
    }

    public byte[]? GetBinary(string keyPath, string valueName)
    {
        using var key = Registry.CurrentUser.OpenSubKey(keyPath);
        return key?.GetValue(valueName) as byte[];
    }

    public void SetString(string keyPath, string valueName, string value)
    {
        using var key = Registry.CurrentUser.CreateSubKey(keyPath, writable: true);
        key.SetValue(valueName, value, RegistryValueKind.String);
    }

    public void DeleteValue(string keyPath, string valueName)
    {
        using var key = Registry.CurrentUser.OpenSubKey(keyPath, writable: true);
        key?.DeleteValue(valueName, throwOnMissingValue: false);
    }
}
