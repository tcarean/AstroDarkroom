# Mindfulness Reminder

A tray-resident break reminder for Windows. It shows an always-on-top reminder window on
every workstation unlock, and at a configurable interval while the user works -- unless the
user is on a call or in a full-screen application, in which case it waits. The two reminders
have a switch and a message each, and both say "Ground yourself" until changed. .NET 8,
**zero external dependencies**: WPF for the windows, Windows Forms for the tray icon, its menu
and `Screen`, both from the WindowsDesktop shared framework (ADR 0007).

## Commands

- Gate (what pre-commit, pre-merge-commit and the Claude commit hook run, about 20 s):
  `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify.ps1`
- Fast loop, no tests: `... scripts/verify.ps1 -SkipTests`
- Build: `dotnet build MindfulnessReminder.sln -c Release --nologo` (Windows only: WPF). The
  SDK is 9.0.318 or a later 9.0.3xx, pinned in `global.json`; the projects target .NET 8
- Test: `dotnet test MindfulnessReminder.sln -c Release --nologo`
- One test: `dotnet test MindfulnessReminder.sln --filter "FullyQualifiedName~R024"`
- Format: `dotnet format MindfulnessReminder.sln` (check only: `--verify-no-changes`)
- See the reminder without locking the machine:
  `src\MindfulnessReminder.App\bin\Release\net8.0-windows\MindfulnessReminder.exe --preview ["text"]`
  (no tray icon, no registry, no timer; with a text, that text after the same normalization a
  saved one gets, R-045)
- See what the pause checks detect: `... MindfulnessReminder.exe --detect [seconds]`; with a
  duration it reports a timeline, the only way to test the full-screen check (R-080 to R-082).
  Neither switch claims the single-instance mutex, so both run beside the copy in the tray.
- Design notes a branch touched (the index and Covers paths are a gate step):
  `scripts/check-design.ps1 -Since main`
- Mutation score of Core (minutes, not in the gate): `scripts/mutate.ps1 [-Mutate "**/IntervalSchedule.cs"]`
- Release (verify, version in `Directory.Build.props`, CHANGELOG, publish; no commit, no tag):
  `scripts/release.ps1 -Version X.Y.Z [-WhatIf]`
- Take the kit's shared folders and settings keys after a change there: `scripts/sync.ps1`
  (`-Check` only compares)
- There are no validation figures: `docs/validation.json` lists no case (ADR 0005), so
  `/validate` has nothing to run.

## Layout

- `src/MindfulnessReminder.Core/` - `net8.0`, no UI dependency: every rule the app has
  (`ReminderCoordinator.cs`, `IntervalSchedule.cs`, `ReminderSettings.cs`,
  `SuppressionPolicy.cs`, `TrayTooltip.cs`, `DetectionLog.cs`, the settings store, auto-start,
  placement, version and licence) and the seams the adapters fill. All of it unit tested.
- `src/MindfulnessReminder.App/` - `net8.0-windows`, `WinExe`, the binary is
  `MindfulnessReminder.exe`: the three windows, the tray icon, the dark theme, and one Windows
  API per file behind a Core interface. Nothing here decides anything; none of it is
  unit-tested (ADR 0008).
- `tests/MindfulnessReminder.Tests/` - `net8.0`, references **Core only**: every rule on
  fakes, `RepoInvariantTests`, `LicenseHeaderTests`, `VersionTests`. `tests/testdata/` holds
  an empty manifest: there is no test data.
- `Directory.Build.props` - `<Version>` and `<Copyright>`, the single place for both (ADR
  0011), and the strictness every project builds with (ADR 0003).
- `kit.json` - what the kit's scripts, skills and agent need to know about this project.
  `docs/skills.md` - this project's additions to the kit's skills. `scripts/`,
  `.claude/skills/`, `.claude/agents/`, `.claude/rules/kit/` and the process keys of
  `.claude/settings.json` are the kit's, the same here (ADR 0002; the kit's rule "Shared
  folders").
- `docs/requirements.md` - the behaviour, with IDs. `docs/decisions/` - ADRs.
  `docs/validation.md` + `.json` - no figures, and why. `docs/design/` - the explanations, per
  area, read on demand. `docs/plans/` - approved plans. `README.md` - the user documentation,
  English.
- The process rules are the kit's, in `.claude/rules/kit/process.md`; this project's own are in
  this file. The reasons behind the process are in `GHID.md` in the kit folder
  `ClaudeCodeStarterKit` beside this project in the `photo` repository (for people, not loaded).
- This folder lives inside the `photo` monorepo (`E:\Photografie\Code`); the git root is the
  parent. The root `.gitignore` has a `*.app` rule that would hide `src/MindfulnessReminder.App/`;
  this project's `.gitignore` un-ignores it, keep that line (ADR 0006).

## Rules

The process rules every project made from the kit follows are in `.claude/rules/kit/process.md`,
loaded with this file (the kit's ADR 0028). These are Mindfulness Reminder's own; one that
extends or varies a kit rule names it.

- **All text is in English** (extends the kit's rule "Languages"): UI strings, comments,
  documentation, and the README too. The sibling `FlatCheck` tool is Romanian; that convention
  is its own, do not carry it over (ADR 0004).
- **Zero external dependencies** (extends the kit's rule "Packages"). `NotifyIcon` and
  everything else come from the shared framework; WPF having no numeric spinner is not a
  reason to add a package, the settings dialog hand-validates a `TextBox` (ADR 0007).
- **Logic in Core, adapters in App.** If a new rule cannot be tested because it sits in the
  App project, it is in the wrong project; the test project deliberately does not reference
  App, so the build enforces it (ADR 0008).
- **Licence headers.** Every `.cs` and `.xaml` file starts with the GPL notice, new files
  included; `LicenseHeaderTests` fails otherwise. XML comments cannot contain `--`, so the
  em-dash of C# comments is reworded in XAML (ADR 0009).
- **One timer, one deadline, on the wall clock.** The timer ticks every second and
  `IntervalSchedule` compares a deadline with `IClock.UtcNow`; never a timer armed for the
  interval, never a count of ticks. A lock suspends the schedule and an unlock restarts it in
  full, before the debounce and the already-showing check (ADR 0012 to ADR 0014).
- **Read the device, not the window title.** A call is the `ConsentStore` capture record;
  full screen is `SHQueryUserNotificationState` mapped in Core, and `QUNS_NOT_PRESENT` must
  not suppress. Do not reintroduce a table of window titles (ADR 0016, ADR 0017).
- **A periodic reminder is postponed, an unlock reminder is skipped**, and there is no cap on
  how long one is held back; the tooltip carries the reason (ADR 0018).
- **The activity monitor is never read on the one-second tick**: when a reminder is due, once
  per retry, and every 10 s idle so the tooltip is current (ADR 0019).
- **The coordinator picks the text, the presenter draws it.** `BringToFront` takes nothing;
  messages are normalized like numbers are clamped, and the dialog refuses a blank one only
  for a reminder that is on (ADR 0022, ADR 0023).
- **The reminder window is light and swallows Enter.** `BreakWindow` takes no dark theme, its
  Close button is not `IsDefault`, and Enter is handled in `OnPreviewKeyDown`; Settings and
  About are modeless, never `ShowDialog` (ADR 0026, ADR 0030).
- **The dark theme is merged per window, never into `Application.Resources`.** Every template
  carries its own disabled trigger; the palette is repeated in `DarkMenuRenderer` and never
  moves into Core (ADR 0030, ADR 0031).
- **Settings are strings, read forgivingly.** Eight string values under
  `HKCU\Software\MindfulnessReminder`, clamped or defaulted on reading, never rejected; the
  Settings dialog is the one place that refuses (ADR 0027).
- **The tray tooltip fits 63 characters.** `TrayTooltip` is the pure function that proves it
  (ADR 0025).
- **User documentation lives in `README.md`, and only there.** It is the single source for it;
  `docs/` explains what the code does and why, for whoever maintains it (ADR 0034).

## Workflow

The kit's workflow in `.claude/rules/kit/process.md` applies. Mindfulness Reminder adds:

- `/wrap-up` checks `README.md`, the user documentation. A change under
  `src/MindfulnessReminder.App/` is looked at with `--preview` (the reminder window only) or
  `--detect` (the activity monitor only); anything else of the App runs only in a full tray
  start, which from a worktree build repairs the auto-start entry to that build (R-006), so it
  is listed for the owner to look at in their own copy after the merge (ADR 0008).
- A release is tagged `mindfulnessreminder-vX.Y.Z`, the name `scripts/release.ps1` prints; it
  publishes the application framework-dependent into `artifacts/X.Y.Z/` (ADR 0002).
- The main checkout of the kit's rule "Worktrees" is the shared checkout `E:\Photografie\Code`.

## Read before touching

`docs/design/README.md` maps every area to its note. The ones most likely to catch a careless
edit:

- `IntervalSchedule.cs`, `ReminderCoordinator.cs` or the tick: `docs/design/periodic-reminder.md`
  and ADR 0012 to ADR 0014.
- `HandleTick`, `SuppressionPolicy`, `WindowsActivityMonitor`, `--detect`:
  `docs/design/pausing.md` and ADR 0016 to ADR 0020.
- `ReminderSettings`, `HandleUnlock`, the messages: `docs/design/two-reminders.md` and ADR
  0021 to ADR 0024.
- Any XAML, a new window: `docs/design/dark-theme.md` and `docs/design/windows.md`, ADR 0026,
  ADR 0030 to ADR 0032.
- A new setting: `docs/design/settings-storage.md` and ADR 0027.
- `Version.cs`, `LicenseInfo.cs`, `LicenseHeaderTests`: `docs/design/versioning-and-licence.md`,
  ADR 0009 to ADR 0011.
- A file you have not opened before: `docs/design/code-map.md`.

## Style

- `.editorconfig` holds whitespace rules and `IDE0005`; `dotnet format` enforces them, and so
  does the build, which writes the XML documentation file for that reason (CS1591 muted).
  Warnings are errors, `EnforceCodeStyleInBuild` and `Nullable` are on, in
  `Directory.Build.props` (ADR 0003). The kit's code-style rules and the analyzers are not
  enabled: 374 analyzer findings on 2026-10-09, 352 of them the underscores in test names; a
  style pass is a task of its own.
- Test names are sentences with underscores, `R###_` first.
- Fix, do not suppress, unless the suppression carries a comment with the reason.
