# Changelog

All notable changes to this project. Format: [Keep a Changelog](https://keepachangelog.com/en/1.1.0/);
versions follow [SemVer](https://semver.org/). The version number itself lives in
`Directory.Build.props`; the owner bumps it.

## [Unreleased]

### Internal

- The project follows the ClaudeCodeStarterKit process (ADR 0001 to 0006): `scripts/verify.ps1`
  is the gate (the kit's scripts, skills, agent and rules synced, `kit.json`, the test data
  manifest, `dotnet format`, warnings as errors, the tests), package versions live once in
  `Directory.Packages.props` with lock files, and the strictness every project builds with
  moved from the three csproj files to `Directory.Build.props`. `CLAUDE.md` was split into
  `docs/requirements.md` (55 rows with IDs), `docs/decisions/` (34 ADRs, 28 of them
  retroactive) and `docs/design/` (9 notes, moved unchanged); `CLAUDE.md` holds only the
  project's rules. `RepoInvariantTests` ties `implemented` rows to tests named `R###_...`
  (176 test methods renamed for 42 rows). There are no validation figures, and
  `docs/validation.md` says why. No behaviour changed; one `dotnet format` pass removed six
  unused usings in five files.

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
