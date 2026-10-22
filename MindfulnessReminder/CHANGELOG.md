# Changelog

All notable changes to this project. Format: [Keep a Changelog](https://keepachangelog.com/en/1.1.0/);
versions follow [SemVer](https://semver.org/). The version number itself lives in
`Directory.Build.props`; the owner bumps it.

## [Unreleased]

### Fixed

- With the periodic reminder off, an unlock during a call no longer leaves the tray tooltip
  saying "paused" after the call has ended (R-060).
- A registry key the account may not read or write no longer stops the application at start:
  the settings fall back to their defaults, and auto-start is registered at a later start
  (R-003, R-051).
- An error in a tray-menu action now ends the application with its own "Mindfulness Reminder
  stopped" dialog, instead of the Windows Forms error dialog (R-076).
- A first run that Windows cuts short after writing the startup entry no longer leaves that
  entry behind without its record, which made every later start register it again and undid
  an untick (R-003).
- A first run that finds a startup entry already there now repairs its path and keeps a Task
  Manager veto, as later runs do; it used to lift the veto, at every start while the app's own
  registry key refused writes (R-003).
- A startup entry or a Task Manager veto the account may not read no longer ends the
  application at start or when the tray menu opens: such an entry counts as off, such a veto as
  no veto (R-004).
- Ticking "Run at Windows startup" no longer reports that Windows refused the change, while the
  entry was in fact written, when Task Manager's approval key refuses access: only a veto the
  account can read is lifted (R-005).

### Internal

- The project follows the ClaudeCodeStarterKit process (ADR 0001 to 0006): `scripts/verify.ps1`
  is the gate (the kit's scripts, skills, agent and rules synced, `kit.json`, the test data
  manifest, `dotnet format`, warnings as errors, the tests), package versions live once in
  `Directory.Packages.props` with lock files, and the strictness every project builds with
  moved from the three csproj files to `Directory.Build.props`; `global.json` pins the SDK to
  9.0.318 or a later 9.0.3xx (the projects stay on .NET 8). `CLAUDE.md` was split into
  `docs/requirements.md` (55 rows with IDs), `docs/decisions/` (34 ADRs, 28 of them
  retroactive) and `docs/design/` (9 notes, moved unchanged); `CLAUDE.md` holds only the
  project's rules. `RepoInvariantTests` ties `implemented` rows to tests named `R###_...`
  (176 test methods renamed for 42 rows). There are no validation figures, and
  `docs/validation.md` says why. No behaviour changed; one `dotnet format` pass removed six
  unused usings in five files.
- Every type and method in the code has an XML documentation comment, with a `<param>` for
  each parameter and a `<returns>` for each value (the kit's rule "Documentation comments",
  the kit's ADR 0035): 8 types and 150 methods documented or completed, 12 of them through
  `<inheritdoc/>`. Comments that no longer matched the code were corrected along the way. No
  code changed.

## [1.4.0] - 2026-09-14

The state of the tool when the changelog was started, built between 2026-08-28 and 2026-09-14
in eight commits:

- 1.0.0 (2026-09-01, after the base of 2026-08-28): the reminder window on every unlock,
  centred on the primary monitor; the tray icon with its four-item menu; auto-start under the
  Run key, registered on the first run only, with the Task Manager veto read and a stale path
  repaired; the GPL notice on every source file, the version and copyright from
  `Directory.Build.props`, About.
- 1.1.0 (2026-09-08): a periodic reminder every N minutes while working, one timer driving a
  wall-clock deadline, suspended across a lock; the tooltip countdown; Settings in the tray
  menu; Enter no longer closes the reminder.
- 1.2.0 (2026-09-08): a reminder waits for a call (the microphone or camera in use) or a
  full-screen application; the two pauses and the retry gap in Settings; the tooltip says why
  a reminder is held back; `--detect` and `--detect <seconds>`.
- 1.3.0 (2026-09-08): the Settings window, About and the tray menu are dark; About is a
  window; the application icon.
- 1.4.0 (2026-09-14): a switch and a message for each of the two reminders; messages
  normalized and capped at 50 characters, scaled down to fit the window; `--preview "text"`.

No earlier version was released; the eight commits are in `git log`.
