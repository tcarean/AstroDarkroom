using MindfulnessReminder.Core;

namespace MindfulnessReminder.Tests;

public class StartupRegistrarTests
{
    private const string ValueName = "MindfulnessReminder";
    private const string ExePath = @"C:\Apps\MindfulnessReminder\MindfulnessReminder.exe";

    private readonly FakeRegistryStore _registry = new();

    [Fact]
    public void First_run_registers_auto_start_and_records_the_marker()
    {
        var autoStart = new RegistryAutoStartManager(_registry, ValueName, ExePath);

        Assert.True(new StartupRegistrar(_registry, autoStart).EnsureRegisteredOnFirstRun());

        Assert.Equal(AutoStartStatus.Enabled, autoStart.GetStatus());
        Assert.Equal("1", _registry.GetString(StartupRegistrar.SettingsKeyPath, StartupRegistrar.RegisteredMarkerValueName));
    }

    [Fact]
    public void Later_runs_do_not_re_register_after_the_user_turned_auto_start_off()
    {
        var autoStart = new RegistryAutoStartManager(_registry, ValueName, ExePath);
        var registrar = new StartupRegistrar(_registry, autoStart);

        registrar.EnsureRegisteredOnFirstRun();
        autoStart.Disable();

        Assert.False(registrar.EnsureRegisteredOnFirstRun());
        Assert.Equal(AutoStartStatus.Disabled, autoStart.GetStatus());
    }

    [Fact]
    public void Later_runs_repair_the_path_when_the_app_has_moved()
    {
        new StartupRegistrar(_registry, new RegistryAutoStartManager(_registry, ValueName, @"D:\Old\App.exe"))
            .EnsureRegisteredOnFirstRun();

        var moved = new RegistryAutoStartManager(_registry, ValueName, ExePath);

        Assert.False(new StartupRegistrar(_registry, moved).EnsureRegisteredOnFirstRun());
        Assert.Equal($"\"{ExePath}\"", _registry.GetString(RegistryAutoStartManager.RunKeyPath, ValueName));
    }

    [Fact]
    public void Later_runs_do_not_lift_a_Task_Manager_veto()
    {
        var autoStart = new RegistryAutoStartManager(_registry, ValueName, ExePath);
        var registrar = new StartupRegistrar(_registry, autoStart);
        registrar.EnsureRegisteredOnFirstRun();
        _registry.SetBinary(
            RegistryAutoStartManager.StartupApprovedKeyPath,
            ValueName,
            [0x03, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0]);

        registrar.EnsureRegisteredOnFirstRun();

        Assert.Equal(AutoStartStatus.BlockedByWindows, autoStart.GetStatus());
    }
}
