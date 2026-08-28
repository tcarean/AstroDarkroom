namespace MindfulnessReminder.Core;

/// <summary>
/// Decides what auto-start should do on each launch.
///
/// The requirement is that the app registers itself for Windows startup, but that must not
/// mean "re-register on every launch" -- that would silently undo the user unticking the
/// tray checkbox. So registration happens once, on the very first run, and is recorded by a
/// marker value. Later launches only repair a stale executable path.
/// </summary>
public sealed class StartupRegistrar
{
    public const string SettingsKeyPath = @"Software\MindfulnessReminder";
    public const string RegisteredMarkerValueName = "AutoStartInitialized";

    private readonly IRegistryStore _registry;
    private readonly IAutoStartManager _autoStart;

    public StartupRegistrar(IRegistryStore registry, IAutoStartManager autoStart)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(autoStart);

        _registry = registry;
        _autoStart = autoStart;
    }

    /// <summary>
    /// Returns <c>true</c> when this call performed the initial registration.
    /// </summary>
    public bool EnsureRegisteredOnFirstRun()
    {
        if (_registry.GetString(SettingsKeyPath, RegisteredMarkerValueName) is not null)
        {
            _autoStart.RepairPathIfNeeded();
            return false;
        }

        _autoStart.Enable();
        _registry.SetString(SettingsKeyPath, RegisteredMarkerValueName, "1");
        return true;
    }
}
