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

/// <summary>
/// The lines of the decision log that do not come out of the coordinator: the line format and
/// the rotation rule (R-110), the start line (R-111), the settings and auto-start lines (R-113)
/// and the crash line (R-114). The coordinator's lines (R-112) are in ReminderCoordinatorTests,
/// on its fakes.
/// </summary>
public class DecisionLogTests
{
    private const string Banner = "Mindfulness Reminder 1.4.0";

    private const string DefaultSummary =
        "periodic on every 30 min, unlock on, pause for mic on, pause for full screen on, retry 20 s, "
        + "messages \"Ground yourself\" / \"Ground yourself\"";

    private const string ValueName = "MindfulnessReminder";
    private const string ExePath = @"C:\Apps\MindfulnessReminder\MindfulnessReminder.exe";

    private readonly FakeDecisionLog _log = new();
    private readonly FakeRegistryStore _registry = new();

    // ----- R-110: the line and the rotation rule -----

    [Fact]
    public void R110_A_line_is_the_local_time_two_spaces_and_the_text()
    {
        var at = new DateTimeOffset(2026, 10, 26, 8, 17, 9, TimeSpan.FromHours(2));

        var line = DecisionLog.FormatLine(at, "Periodic reminder shown: Ground yourself");

        Assert.Equal("2026-10-26 08:17:09  Periodic reminder shown: Ground yourself", line);
    }

    [Fact]
    public void R110_Line_breaks_in_the_text_become_separators_so_an_event_stays_on_one_line()
    {
        var at = new DateTimeOffset(2026, 10, 26, 8, 21, 1, TimeSpan.FromHours(2));

        var line = DecisionLog.FormatLine(at, "first\nsecond\r\nthird\n\nfifth");

        Assert.Equal("2026-10-26 08:21:01  first | second | third |  | fifth", line);
    }

    [Fact]
    public void R110_The_file_rotates_once_it_holds_more_than_the_cap()
    {
        Assert.Equal(262_144, DecisionLog.MaxBytes);
        Assert.False(DecisionLog.ShouldRotate(0));
        Assert.False(DecisionLog.ShouldRotate(DecisionLog.MaxBytes));
        Assert.True(DecisionLog.ShouldRotate(DecisionLog.MaxBytes + 1));
    }

    [Fact]
    public void R110_A_null_text_is_refused()
    {
        Assert.Throws<ArgumentNullException>(() => DecisionLog.FormatLine(DateTimeOffset.Now, null!));
    }

    // ----- R-111: the start line -----

    [Fact]
    public void R111_The_start_line_names_the_build_the_runtime_the_arguments_and_the_registration()
    {
        var line = DecisionLog.Started(Banner, ".NET 8.0.33", new[] { "--restarted" }, 0);

        Assert.Equal(
            "Started Mindfulness Reminder 1.4.0 on .NET 8.0.33, arguments: --restarted; restart registration: ok",
            line);
    }

    [Fact]
    public void R111_No_arguments_read_as_none_and_a_failed_hresult_is_shown_in_hex()
    {
        var line = DecisionLog.Started(Banner, ".NET 8.0.33", Array.Empty<string>(), unchecked((int)0x80070005));

        Assert.Equal(
            "Started Mindfulness Reminder 1.4.0 on .NET 8.0.33, arguments: (none); restart registration: refused (0x80070005)",
            line);
    }

    [Fact]
    public void R111_Several_arguments_are_joined_by_one_space()
    {
        var line = DecisionLog.Started(Banner, ".NET 8.0.33", new[] { "--restarted", "extra" }, 0);

        Assert.Contains("arguments: --restarted extra;", line);
    }

    // ----- R-113: the settings lines -----

    [Fact]
    public void R113_The_summary_reads_every_setting_in_one_line()
    {
        Assert.Equal(DefaultSummary, DecisionLog.Summary(ReminderSettings.Default));

        var changed = new ReminderSettings(false, 45)
        {
            UnlockEnabled = false,
            UnlockMessage = "Breathe",
            PeriodicMessage = "Stretch",
            PauseWhileMicrophoneInUse = false,
            PauseWhileFullScreen = true,
            RetrySeconds = 60,
        };

        Assert.Equal(
            "periodic off every 45 min, unlock off, pause for mic off, pause for full screen on, retry 60 s, "
            + "messages \"Breathe\" / \"Stretch\"",
            DecisionLog.Summary(changed));
    }

    [Fact]
    public void R113_Loading_an_empty_registry_names_every_value_that_fell_back()
    {
        new RegistrySettingsStore(_registry, _log).Load();

        Assert.Equal(
            [
                "Settings loaded: " + DefaultSummary + "; defaults used for PeriodicRemindersEnabled, "
                + "PeriodicIntervalMinutes, UnlockReminderEnabled, UnlockMessage, PeriodicMessage, "
                + "PauseWhileMicrophoneInUse, PauseWhileFullScreen, PauseRetrySeconds",
            ],
            _log.Lines);
    }

    [Fact]
    public void R113_Loading_a_full_registry_names_nothing_and_a_clamped_number_is_reported()
    {
        var store = new RegistrySettingsStore(_registry, _log);
        store.Save(ReminderSettings.Default);
        _log.Clear();
        _registry.SetString(RegistrySettingsStore.KeyPath, RegistrySettingsStore.IntervalValueName, "0");

        store.Load();

        Assert.Equal(
            ["Settings loaded: " + DefaultSummary.Replace("every 30 min", "every 1 min") + "; clamped PeriodicIntervalMinutes 0 -> 1"],
            _log.Lines);
    }

    [Fact]
    public void R113_Loading_a_complete_registry_writes_the_summary_alone()
    {
        var store = new RegistrySettingsStore(_registry, _log);
        store.Save(ReminderSettings.Default);
        _log.Clear();

        store.Load();

        Assert.Equal(["Settings loaded: " + DefaultSummary], _log.Lines);
    }

    [Fact]
    public void R113_A_registry_that_refuses_to_be_read_says_so()
    {
        _registry.ReadRefusal = new UnauthorizedAccessException();

        new RegistrySettingsStore(_registry, _log).Load();

        Assert.Equal(
            ["Settings loaded: " + DefaultSummary + "; the registry refused to be read, every default used"],
            _log.Lines);
    }

    [Fact]
    public void R113_Saving_writes_the_summary()
    {
        new RegistrySettingsStore(_registry, _log).Save(ReminderSettings.Default);

        Assert.Equal(["Settings saved: " + DefaultSummary], _log.Lines);
    }

    // ----- R-113: the auto-start lines -----

    [Fact]
    public void R113_The_first_run_logs_the_registration_with_the_quoted_path()
    {
        var autoStart = new RegistryAutoStartManager(_registry, ValueName, ExePath);

        new StartupRegistrar(_registry, autoStart, _log).EnsureRegisteredOnFirstRun();

        Assert.Equal(["Auto-start registered: \"" + ExePath + "\""], _log.Lines);
    }

    [Fact]
    public void R113_A_later_run_that_repairs_the_path_logs_the_repair()
    {
        _registry.SetString(StartupRegistrar.SettingsKeyPath, StartupRegistrar.RegisteredMarkerValueName, "1");
        _registry.SetString(RegistryAutoStartManager.RunKeyPath, ValueName, "\"D:\\Old\\App.exe\"");
        var autoStart = new RegistryAutoStartManager(_registry, ValueName, ExePath);

        new StartupRegistrar(_registry, autoStart, _log).EnsureRegisteredOnFirstRun();

        Assert.Equal(["Auto-start entry repaired to \"" + ExePath + "\""], _log.Lines);
    }

    [Fact]
    public void R113_A_later_run_with_nothing_to_do_logs_nothing()
    {
        _registry.SetString(StartupRegistrar.SettingsKeyPath, StartupRegistrar.RegisteredMarkerValueName, "1");
        var autoStart = new RegistryAutoStartManager(_registry, ValueName, ExePath);
        _registry.SetString(RegistryAutoStartManager.RunKeyPath, ValueName, autoStart.Command);

        new StartupRegistrar(_registry, autoStart, _log).EnsureRegisteredOnFirstRun();

        Assert.Empty(_log.Lines);
    }

    [Fact]
    public void R113_A_refused_registration_logs_the_refusal_by_its_type()
    {
        _registry.WriteRefusal = new UnauthorizedAccessException();
        var autoStart = new RegistryAutoStartManager(_registry, ValueName, ExePath);

        new StartupRegistrar(_registry, autoStart, _log).EnsureRegisteredOnFirstRun();

        Assert.Equal(["Auto-start: the registry refused (UnauthorizedAccessException), no registration made"], _log.Lines);
    }

    // ----- R-114: the crash line -----

    [Fact]
    public void R114_The_crash_line_carries_the_dialog_text_on_one_line()
    {
        const string name = "UIAutomationTypes, Version=8.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35";
        var exception = new FileNotFoundException(
            "Could not load file or assembly '" + name + "'. The system cannot find the file specified.",
            name);
        var at = new DateTimeOffset(2026, 10, 26, 8, 17, 9, TimeSpan.FromHours(2));

        var line = DecisionLog.FormatLine(at, DecisionLog.Crashed(CrashReport.Format(exception, Banner)));

        Assert.Equal(
            "2026-10-26 08:17:09  Crashed: System.IO.FileNotFoundException: Could not load file or assembly '"
            + name + "'. The system cannot find the file specified. | File: " + name
            + " |  | " + Banner,
            line);
    }
}
