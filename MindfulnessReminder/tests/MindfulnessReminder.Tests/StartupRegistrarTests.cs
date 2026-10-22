// SPDX-License-Identifier: GPL-3.0-or-later
//
// Copyright (C) 2026 Tudor Carean
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <https://www.gnu.org/licenses/>.

using MindfulnessReminder.Core;

namespace MindfulnessReminder.Tests;

public class StartupRegistrarTests
{
    private const string ValueName = "MindfulnessReminder";
    private const string ExePath = @"C:\Apps\MindfulnessReminder\MindfulnessReminder.exe";

    private readonly FakeRegistryStore _registry = new();

    [Fact]
    public void R003_First_run_registers_auto_start_and_records_the_marker()
    {
        var autoStart = new RegistryAutoStartManager(_registry, ValueName, ExePath);

        Assert.True(new StartupRegistrar(_registry, autoStart).EnsureRegisteredOnFirstRun());

        Assert.Equal(AutoStartStatus.Enabled, autoStart.GetStatus());
        Assert.Equal("1", _registry.GetString(StartupRegistrar.SettingsKeyPath, StartupRegistrar.RegisteredMarkerValueName));
    }

    /// <summary>
    /// A policy that refuses the Run key, or the app's own key, must not stop the app from
    /// starting: nothing is registered and no marker is written, so the next start tries again.
    /// </summary>
    [Fact]
    public void R003_A_refused_registration_does_not_stop_the_app()
    {
        _registry.WriteRefusal = new UnauthorizedAccessException();
        var autoStart = new RegistryAutoStartManager(_registry, ValueName, ExePath);

        Assert.False(new StartupRegistrar(_registry, autoStart).EnsureRegisteredOnFirstRun());
        Assert.False(_registry.Contains(StartupRegistrar.SettingsKeyPath, StartupRegistrar.RegisteredMarkerValueName));
    }

    /// <summary>
    /// The Run key may take the write while the next one is refused: the Task Manager key's,
    /// lifting a veto left without its Run value, or the marker's. The value already written
    /// must not stay behind without its marker: every later start would then register it
    /// again, undoing an untick.
    /// </summary>
    [Theory]
    [InlineData(StartupRegistrar.SettingsKeyPath)]
    [InlineData(RegistryAutoStartManager.StartupApprovedKeyPath)]
    public void R003_A_refusal_after_the_run_value_is_written_leaves_nothing_registered(string refusedKey)
    {
        SetApproval(0x03);
        _registry.WriteRefusal = new UnauthorizedAccessException();
        _registry.RefusedKeyPath = refusedKey;
        var autoStart = new RegistryAutoStartManager(_registry, ValueName, ExePath);

        Assert.False(new StartupRegistrar(_registry, autoStart).EnsureRegisteredOnFirstRun());
        Assert.False(_registry.Contains(RegistryAutoStartManager.RunKeyPath, ValueName));
        Assert.False(_registry.Contains(StartupRegistrar.SettingsKeyPath, StartupRegistrar.RegisteredMarkerValueName));
    }

    /// <summary>
    /// An approval key the account may not read is no veto (R-004), and Enable leaves it alone
    /// (R-005), so the first run registers although that key refuses every access.
    /// </summary>
    [Fact]
    public void R003_A_Task_Manager_key_the_account_may_not_read_does_not_stop_the_registration()
    {
        _registry.ReadRefusal = new System.Security.SecurityException();
        _registry.RefusedKeyPath = RegistryAutoStartManager.StartupApprovedKeyPath;
        var autoStart = new RegistryAutoStartManager(_registry, ValueName, ExePath);

        Assert.True(new StartupRegistrar(_registry, autoStart).EnsureRegisteredOnFirstRun());
        Assert.Equal(AutoStartStatus.Enabled, autoStart.GetStatus());
        Assert.Equal("1", _registry.GetString(StartupRegistrar.SettingsKeyPath, StartupRegistrar.RegisteredMarkerValueName));
    }

    /// <summary>
    /// A Task Manager key that refuses writes is no obstacle while there is no veto to lift:
    /// Enable touches it only to delete a veto it can read.
    /// </summary>
    [Fact]
    public void R003_A_Task_Manager_key_that_refuses_writes_does_not_stop_the_registration_without_a_veto()
    {
        _registry.WriteRefusal = new UnauthorizedAccessException();
        _registry.RefusedKeyPath = RegistryAutoStartManager.StartupApprovedKeyPath;
        var autoStart = new RegistryAutoStartManager(_registry, ValueName, ExePath);

        Assert.True(new StartupRegistrar(_registry, autoStart).EnsureRegisteredOnFirstRun());
        Assert.Equal(AutoStartStatus.Enabled, autoStart.GetStatus());
    }

    /// <summary>
    /// The first run registers only where there is no entry. One already there is repaired and
    /// keeps its veto, the marker is written, and the call reports no registration, so no
    /// balloon claims that the app now starts with Windows.
    /// </summary>
    [Fact]
    public void R003_A_first_run_that_finds_an_entry_repairs_it_and_keeps_its_veto()
    {
        _registry.SetString(RegistryAutoStartManager.RunKeyPath, ValueName, @"""D:\Old\App.exe""");
        SetApproval(0x03);
        var autoStart = new RegistryAutoStartManager(_registry, ValueName, ExePath);

        Assert.False(new StartupRegistrar(_registry, autoStart).EnsureRegisteredOnFirstRun());
        Assert.Equal(autoStart.Command, _registry.GetString(RegistryAutoStartManager.RunKeyPath, ValueName));
        Assert.Equal(AutoStartStatus.BlockedByWindows, autoStart.GetStatus());
        Assert.Equal("1", _registry.GetString(StartupRegistrar.SettingsKeyPath, StartupRegistrar.RegisteredMarkerValueName));
    }

    /// <summary>
    /// A Run value that was there before the first run was not written by it, so a refusal of
    /// the marker must not take it away, nor lift its veto. While that key refuses writes,
    /// every start is a first run, and a switch-off in Task Manager must survive each one.
    /// </summary>
    [Fact]
    public void R003_A_refused_first_run_repairs_an_entry_it_found_and_keeps_its_veto()
    {
        _registry.SetString(RegistryAutoStartManager.RunKeyPath, ValueName, @"""D:\Old\App.exe""");
        SetApproval(0x03);
        _registry.WriteRefusal = new UnauthorizedAccessException();
        _registry.RefusedKeyPath = StartupRegistrar.SettingsKeyPath;
        var autoStart = new RegistryAutoStartManager(_registry, ValueName, ExePath);

        Assert.False(new StartupRegistrar(_registry, autoStart).EnsureRegisteredOnFirstRun());
        Assert.Equal(autoStart.Command, _registry.GetString(RegistryAutoStartManager.RunKeyPath, ValueName));
        Assert.Equal(AutoStartStatus.BlockedByWindows, autoStart.GetStatus());
    }

    /// <summary>
    /// The app's own key may refuse to be read as well as written. Any of the three refusals of
    /// a read (R-051) leaves the app starting, with nothing registered.
    /// </summary>
    [Theory]
    [InlineData(typeof(System.Security.SecurityException))]
    [InlineData(typeof(UnauthorizedAccessException))]
    [InlineData(typeof(IOException))]
    public void R003_A_marker_the_account_may_not_read_does_not_stop_the_app(Type refusal)
    {
        _registry.ReadRefusal = (Exception)Activator.CreateInstance(refusal)!;
        _registry.RefusedKeyPath = StartupRegistrar.SettingsKeyPath;
        var autoStart = new RegistryAutoStartManager(_registry, ValueName, ExePath);

        Assert.False(new StartupRegistrar(_registry, autoStart).EnsureRegisteredOnFirstRun());
        Assert.False(_registry.Contains(RegistryAutoStartManager.RunKeyPath, ValueName));
    }

    /// <summary>
    /// A later run reads the Run value to repair it. A Run key the account may not read leaves
    /// the app starting, whichever of the three refusals of a read it raises.
    /// </summary>
    [Theory]
    [InlineData(typeof(System.Security.SecurityException))]
    [InlineData(typeof(UnauthorizedAccessException))]
    [InlineData(typeof(IOException))]
    public void R006_A_run_value_the_account_may_not_read_does_not_stop_a_later_run(Type refusal)
    {
        new StartupRegistrar(_registry, new RegistryAutoStartManager(_registry, ValueName, @"D:\Old\App.exe"))
            .EnsureRegisteredOnFirstRun();
        _registry.ReadRefusal = (Exception)Activator.CreateInstance(refusal)!;
        _registry.RefusedKeyPath = RegistryAutoStartManager.RunKeyPath;

        var moved = new RegistryAutoStartManager(_registry, ValueName, ExePath);

        Assert.False(new StartupRegistrar(_registry, moved).EnsureRegisteredOnFirstRun());
    }

    [Fact]
    public void R006_A_refused_repair_does_not_stop_the_app()
    {
        new StartupRegistrar(_registry, new RegistryAutoStartManager(_registry, ValueName, @"D:\Old\App.exe"))
            .EnsureRegisteredOnFirstRun();
        _registry.WriteRefusal = new System.Security.SecurityException();

        var moved = new RegistryAutoStartManager(_registry, ValueName, ExePath);

        Assert.False(new StartupRegistrar(_registry, moved).EnsureRegisteredOnFirstRun());
    }

    [Fact]
    public void R003_Later_runs_do_not_re_register_after_the_user_turned_auto_start_off()
    {
        var autoStart = new RegistryAutoStartManager(_registry, ValueName, ExePath);
        var registrar = new StartupRegistrar(_registry, autoStart);

        registrar.EnsureRegisteredOnFirstRun();
        autoStart.Disable();

        Assert.False(registrar.EnsureRegisteredOnFirstRun());
        Assert.Equal(AutoStartStatus.Disabled, autoStart.GetStatus());
    }

    [Fact]
    public void R006_Later_runs_repair_the_path_when_the_app_has_moved()
    {
        new StartupRegistrar(_registry, new RegistryAutoStartManager(_registry, ValueName, @"D:\Old\App.exe"))
            .EnsureRegisteredOnFirstRun();

        var moved = new RegistryAutoStartManager(_registry, ValueName, ExePath);

        Assert.False(new StartupRegistrar(_registry, moved).EnsureRegisteredOnFirstRun());
        Assert.Equal($"\"{ExePath}\"", _registry.GetString(RegistryAutoStartManager.RunKeyPath, ValueName));
    }

    [Fact]
    public void R006_Later_runs_do_not_lift_a_Task_Manager_veto()
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

    private void SetApproval(byte flag) =>
        _registry.SetBinary(
            RegistryAutoStartManager.StartupApprovedKeyPath,
            ValueName,
            [flag, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0]);
}
