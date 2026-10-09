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

public class RegistryAutoStartManagerTests
{
    private const string ValueName = "MindfulnessReminder";
    private const string ExePath = @"C:\Apps\MindfulnessReminder\MindfulnessReminder.exe";

    private readonly FakeRegistryStore _registry = new();

    private RegistryAutoStartManager CreateManager(string exePath = ExePath) =>
        new(_registry, ValueName, exePath);

    [Fact]
    public void R004_GetStatus_returns_disabled_when_no_run_entry_exists()
    {
        Assert.Equal(AutoStartStatus.Disabled, CreateManager().GetStatus());
    }

    [Fact]
    public void R004_GetStatus_returns_disabled_for_a_blank_run_entry()
    {
        _registry.SetString(RegistryAutoStartManager.RunKeyPath, ValueName, "   ");

        Assert.Equal(AutoStartStatus.Disabled, CreateManager().GetStatus());
    }

    [Fact]
    public void R005_GetStatus_returns_enabled_after_Enable()
    {
        var manager = CreateManager();

        manager.Enable();

        Assert.Equal(AutoStartStatus.Enabled, manager.GetStatus());
    }

    [Fact]
    public void R003_Enable_writes_the_quoted_executable_path()
    {
        CreateManager().Enable();

        Assert.Equal(
            $"\"{ExePath}\"",
            _registry.GetString(RegistryAutoStartManager.RunKeyPath, ValueName));
    }

    [Theory]
    [InlineData(0x02)] // what Task Manager writes for an enabled entry
    [InlineData(0x06)]
    public void R004_GetStatus_reports_enabled_when_the_approval_flag_is_even(byte flag)
    {
        var manager = CreateManager();
        manager.Enable();
        SetApproval(flag);

        Assert.Equal(AutoStartStatus.Enabled, manager.GetStatus());
    }

    [Theory]
    [InlineData(0x03)] // what Task Manager writes when the user switches the entry off
    [InlineData(0x01)]
    public void R004_GetStatus_reports_blocked_when_the_approval_flag_is_odd(byte flag)
    {
        var manager = CreateManager();
        manager.Enable();
        SetApproval(flag);

        Assert.Equal(AutoStartStatus.BlockedByWindows, manager.GetStatus());
    }

    [Fact]
    public void R004_GetStatus_ignores_a_Task_Manager_veto_when_no_run_entry_exists()
    {
        SetApproval(0x03);

        Assert.Equal(AutoStartStatus.Disabled, CreateManager().GetStatus());
    }

    [Fact]
    public void R005_Enable_clears_a_Task_Manager_veto()
    {
        var manager = CreateManager();
        manager.Enable();
        SetApproval(0x03);

        manager.Enable();

        Assert.False(_registry.Contains(RegistryAutoStartManager.StartupApprovedKeyPath, ValueName));
        Assert.Equal(AutoStartStatus.Enabled, manager.GetStatus());
    }

    [Fact]
    public void R005_Disable_removes_the_run_entry()
    {
        var manager = CreateManager();
        manager.Enable();

        manager.Disable();

        Assert.False(_registry.Contains(RegistryAutoStartManager.RunKeyPath, ValueName));
        Assert.Equal(AutoStartStatus.Disabled, manager.GetStatus());
    }

    [Fact]
    public void R005_Disable_is_a_no_op_when_nothing_is_registered()
    {
        CreateManager().Disable();

        Assert.Equal(AutoStartStatus.Disabled, CreateManager().GetStatus());
    }

    [Fact]
    public void R006_RepairPathIfNeeded_rewrites_a_stale_path()
    {
        CreateManager(@"D:\Old\MindfulnessReminder.exe").Enable();

        var moved = CreateManager();

        Assert.True(moved.RepairPathIfNeeded());
        Assert.Equal(
            $"\"{ExePath}\"",
            _registry.GetString(RegistryAutoStartManager.RunKeyPath, ValueName));
    }

    [Fact]
    public void R006_RepairPathIfNeeded_leaves_a_matching_path_alone_regardless_of_case()
    {
        CreateManager(ExePath.ToUpperInvariant()).Enable();
        var writesBefore = _registry.WriteCount;

        Assert.False(CreateManager().RepairPathIfNeeded());
        Assert.Equal(writesBefore, _registry.WriteCount);
    }

    [Fact]
    public void R006_RepairPathIfNeeded_does_not_register_an_unregistered_app()
    {
        Assert.False(CreateManager().RepairPathIfNeeded());
        Assert.False(_registry.Contains(RegistryAutoStartManager.RunKeyPath, ValueName));
    }

    [Fact]
    public void R006_RepairPathIfNeeded_keeps_a_Task_Manager_veto_intact()
    {
        CreateManager(@"D:\Old\MindfulnessReminder.exe").Enable();
        SetApproval(0x03);

        var moved = CreateManager();
        Assert.True(moved.RepairPathIfNeeded());

        Assert.Equal(AutoStartStatus.BlockedByWindows, moved.GetStatus());
    }

    [Fact]
    public void R003_Constructor_rejects_a_blank_executable_path()
    {
        Assert.Throws<ArgumentException>(() => new RegistryAutoStartManager(_registry, ValueName, "  "));
    }

    private void SetApproval(byte flag) =>
        _registry.SetBinary(
            RegistryAutoStartManager.StartupApprovedKeyPath,
            ValueName,
            [flag, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0]);
}
