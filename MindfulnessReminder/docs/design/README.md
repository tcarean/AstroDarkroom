# Design notes

One note per area whose code does not explain itself. Each says what the code does, how its
parts fit, and, where it was found out the hard way, why the obvious simpler version is wrong.
`CLAUDE.md` points here under "Read before touching"; nothing in this folder is loaded into a
session unless the task needs it.

Until 2026-10-09 these notes were sections of `CLAUDE.md` ("Structure", "The periodic
reminder", "Pausing for what the user is doing", "Two reminders, two switches, two messages",
"The tray tooltip", "Windows the app shows", "The dark theme", "Settings storage",
"Versioning", "Licensing"); they were moved here unchanged (ADR 0001), with a pointer to the
decision record after each paragraph that became one. Its other sections went elsewhere: the
opening paragraphs and "Conventions" are the rules of the new `CLAUDE.md` and ADR 0004, ADR
0007 to ADR 0009; "Build & test" is its "Commands"; "Committing" is quoted in ADR 0001, which
it superseded. The behaviour the notes describe is also stated, with IDs, in
`../requirements.md`, and for the user in the project's `README.md`. Where a row, a note and an
ADR were written from the same paragraph, the three still say it in their own words; cutting the
notes down to what neither a row nor an ADR says is a task of its own.

**Every note is maintained** (the kit's rule "Design notes"): it says above its first section,
in a **Covers:** sentence, which files it describes, and a change to those files means the note
is updated in the same task, or deleted with the decision it explains. `scripts/check-design.ps1`
checks this index and the Covers paths at every commit; `scripts/check-design.ps1 -Since main`,
run in a task's worktree, lists the notes a branch touched. A Covers sentence names its files as
paths in backticks, from the project root (`src/...`, `tests/...`), and ends at its first full
stop.

| Area | Note | Read before editing |
| --- | --- | --- |
| Every file of the three projects, one line each, and the line between Core and App | `code-map.md` | a file you have not opened before; ADR 0007 and ADR 0008 first |
| The periodic reminder: one timer ticking every second, a deadline on the wall clock, the lock that suspends it and the unlock that restarts it, the clock that moves backwards | `periodic-reminder.md` | `IntervalSchedule.cs`, the tick in `ReminderCoordinator.cs`, `DispatcherPeriodicTimer.cs`; ADR 0012 to ADR 0014 first |
| Holding a reminder back: the capture record for a call, `SHQueryUserNotificationState` for full screen, postponed against skipped, when the monitor is read, `--detect` | `pausing.md` | `SuppressionPolicy.cs`, `ForegroundState.cs`, `WindowsActivityMonitor.cs`, `DetectionLog.cs`, the activity reads in `ReminderCoordinator.cs`; ADR 0016 to ADR 0020 first |
| The two reminders, their switches and messages: the unlock gate, who picks the text, normalization, the fixed window, `--preview` | `two-reminders.md` | `ReminderSettings.cs`, `HandleUnlock`, `IReminderPresenter.cs`, `BreakWindow.xaml`; ADR 0021 to ADR 0024 first |
| The tray tooltip: the 63-character limit, minutes rounded up, the reason instead of the countdown | `tray-tooltip.md` | `TrayTooltip.cs`, `App.UpdateTooltip`; ADR 0025 first |
| The three windows: modality, focus, the show delay, Enter on the reminder | `windows.md` | `BreakWindow`, `SettingsWindow`, `AboutWindow`, `WpfReminderPresenter.cs`; ADR 0026 first |
| The dark theme: merged per window, three surfaces and three mechanisms, the template rules, About as a window | `dark-theme.md` | any XAML, `DarkMenuRenderer.cs`, `ImmersiveDarkMode.cs`, a new window; ADR 0030 to ADR 0032 first |
| The eight registry values, the forgiving read, the clamp | `settings-storage.md` | `RegistrySettingsStore.cs`, a new setting; ADR 0027 first |
| The version and the licence: one source for each, what `VersionTests` and `LicenseHeaderTests` pin, `LICENSE` beside the executable | `versioning-and-licence.md` | `Version.cs`, `LicenseInfo.cs`, `Directory.Build.props`, `VersionTests`, `LicenseHeaderTests`, the `LICENSE` item of the App csproj; ADR 0009 to ADR 0011 first |

The decisions themselves, with the alternatives rejected, are in `../decisions/`. The behaviour,
with IDs, is in `../requirements.md`. Why there are no measured figures is in
`../validation.md`.
