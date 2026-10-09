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
}
