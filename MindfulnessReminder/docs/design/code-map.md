# Code map: every file, one line each

> Moved unchanged on 2026-10-09 (ADR 0001) from `CLAUDE.md`, "Structure". Where a paragraph became a
> decision record, its number is given after it. **Covers:** `src/MindfulnessReminder.Core/`,
> `src/MindfulnessReminder.App/`,
> `tests/MindfulnessReminder.Tests/MindfulnessReminder.Tests.csproj`, `Directory.Build.props`. A
> change to the code this describes means this note is updated in the same task, or deleted with the
> decision it explains. A section this map names in quotation marks is now the note of the same
> subject: "The periodic reminder" is `periodic-reminder.md`, "Pausing for what the user is doing"
> is `pausing.md`, "The tray tooltip" is `tray-tooltip.md`, "The dark theme" is `dark-theme.md`.

## Structure

- `src/MindfulnessReminder.Core/` — `net8.0`, **no UI dependencies**. Every rule the app has
  lives here, which is what makes it unit-testable; the test project references this and
  nothing else.
  - `ReminderCoordinator.cs` — the single place that decides when a reminder appears. Both
    sources of one (an unlock, and the periodic schedule) go through it, so the rules about
    not stacking windows are written once. Owns the debounce, the already-showing check, and
    the `IntervalSchedule`.
  - `IntervalSchedule.cs` — the deadline arithmetic for the periodic reminder. No timer, no
    events, no UI. Read "The periodic reminder" below before touching it.
  - `ReminderSettings.cs` — `record(bool PeriodicEnabled, int IntervalMinutes)` plus six
    init-only properties: the unlock switch, the two messages, the two pauses and their retry
    gap. Also the bounds, `Normalize()`, and `NormalizeMessage()`. Everything that builds
    settings goes through `Normalize`. The properties are init-only rather than more
    positional parameters so that `new ReminderSettings(true, 30)` goes on meaning what it
    always meant.
  - `ISettingsStore.cs` / `RegistrySettingsStore.cs` — settings persistence over
    `IRegistryStore`, under the key the auto-start marker already uses.
  - `TrayTooltip.cs` — the hover text, as a pure function. It is a separate type because of
    the 63-character limit; see "The tray tooltip".
  - `SuppressionPolicy.cs` — `ReminderSuppression` and the pure rule behind the two pause
    settings: which switch governs which signal, and which wins when both apply.
  - `ForegroundState.cs` — the enum, plus `UserNotificationState.FromShellState`, the
    QUERY_USER_NOTIFICATION_STATE table. In Core, taking the raw integer, so the table is
    testable and the adapter has nothing left to get wrong.
  - `ActivitySnapshot.cs` — what the adapter gathers and the policy reads; the line between
    a Windows question and a rule.
  - `DetectionLog.cs` — the timeline `--detect <seconds>` renders. See "Pausing for what the
    user is doing".
  - `IPeriodicTimer.cs`, `IClock.cs`, `ISessionMonitor.cs`, `IReminderPresenter.cs`,
    `IRegistryStore.cs`, `IAutoStartManager.cs`, `IActivityMonitor.cs` — the seams. Each
    exists so a rule can be tested off a real machine; none has a second implementation in
    production.
  - `RegistryAutoStartManager.cs` / `StartupRegistrar.cs` — auto-start over `HKCU\...\Run`,
    plus the once-only registration on first run.
  - `WindowPlacement.cs`, `Version.cs`, `LicenseInfo.cs`, `AutoStartStatus.cs`.
- `src/MindfulnessReminder.App/` — `net8.0-windows`, `WinExe`, `AssemblyName` is
  `MindfulnessReminder`, so the binary is `MindfulnessReminder.exe`. A **thin adapter layer**:
  every file here is either a window or a Windows API behind a Core interface. Nothing here
  decides anything, which is why none of it is unit-tested.
  - `App.xaml.cs` — composition root. `ShutdownMode` is `OnExplicitShutdown`; there is no
    main window. Also holds the single-instance mutex, the crash dialog, the lifetimes of the
    Settings and About windows (both modeless and single-instance; `TrayIconHost` only raises
    the request), and `--preview` / `--detect` — both of which run BEFORE the mutex is
    claimed, so they work alongside the copy already in the tray.
  - `TrayIconHost.cs` — the icon and its four-item menu.
  - `BreakWindow.xaml(.cs)` — the reminder; takes its message in the constructor and has no
    text of its own. `SettingsWindow.xaml(.cs)` — the settings.
    `AboutWindow.xaml(.cs)` — the licence notice, a window rather than a `MessageBox` since
    the dark theme; it displays `LicenseInfo.AboutText` and composes nothing itself.
  - `SystemEventsSessionMonitor.cs`, `DispatcherPeriodicTimer.cs`,
    `WpfReminderPresenter.cs`, `WindowsRegistryStore.cs`, `WindowsActivityMonitor.cs` —
    the adapters. The last one reads the `ConsentStore` capture-usage keys and calls
    `SHQueryUserNotificationState`, and interprets neither.
  - `Theme/DarkTheme.xaml` — the dark palette and the control templates that go with it.
    Merged into `SettingsWindow` **alone** (and into `AboutWindow`, since 432edfd: ADR 0030). See "The dark theme".
  - `ImmersiveDarkMode.cs` — the one P/Invoke that darkens a window's title bar.
  - `DarkMenuRenderer.cs` — the same palette again, for the Windows Forms tray menu.
- `tests/MindfulnessReminder.Tests/` — xUnit, `net8.0`. References **Core only**.
- `README.md` — **the** user-facing documentation, and the single source for it.
- `Directory.Build.props` — `<Version>` and `<Copyright>` for the whole solution.
- `LICENSE` — the verbatim GNU GPL v3 text.

See ADR 0008 for the line between Core and App, ADR 0007 for the hybrid of WPF and Windows Forms,
ADR 0011 and ADR 0010 for the version and the copyright.
