# 0028. Auto-start is one Run value under HKCU, read together with Task Manager's veto, registered on the first run only and repaired on each launch

Date: 2026-10-09
Status: accepted 2026-10-09; Enable deleting the approval value superseded by 0035; the first run registering over an entry already there superseded by 0035

## Context

Retroactive: recorded on 2026-10-09 from `README.md` ("Behaviour", the second bullet; "Deployment
note"; "Design decisions", rows "Auto-start" and "Auto-start truth"), `CLAUDE.md` ("Structure",
the `RegistryAutoStartManager.cs` / `StartupRegistrar.cs` and `AutoStartStatus.cs` lines) and
the comments of `RegistryAutoStartManager.cs`, `StartupRegistrar.cs`, `IAutoStartManager.cs`,
`AutoStartStatus.cs` and `TrayIconHost.cs`; the decision itself dates from 61a0121 (the base
version, 2026-08-28), and its Core files have changed since only by their licence header. The
requirement is that the application registers itself for Windows startup, "but that must not
mean 're-register on every launch' -- that would silently undo the user unticking the tray
checkbox" (`StartupRegistrar`). And the Run key is not the whole truth: Task Manager's Startup
tab "does not delete the value, it records a veto in Explorer\StartupApproved\Run"
(`RegistryAutoStartManager`).

## Decision

- The entry is one value named `MindfulnessReminder` ("also what Task Manager displays") under
  `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, holding the executable's path in quotes:
  per user, so "No elevation", and "one value that maps directly onto a checkbox" (README). The
  path is `Environment.ProcessPath`, or the apphost beside the assembly when that is
  `dotnet.exe`, as under `dotnet run`.
- `RegistryAutoStartManager.GetStatus` reads both places. No Run value, or a blank one, is
  `Disabled`. A Run value whose approval value under
  `HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run` has an odd first
  byte (Task Manager writes `0x03` when the user switches the entry off) is `BlockedByWindows`.
  Anything else, an even first byte (`0x02`) or no approval value at all, is `Enabled`.
- The tray item "Run at Windows startup" is ticked for `Enabled` only, reads "Run at Windows
  startup (disabled in Task Manager)" for `BlockedByWindows`, and is read afresh each time the
  menu opens, never cached. A click disables an `Enabled` entry and enables anything else, and
  `Enable` also deletes the approval value, which "is what Windows treats as 'never vetoed'". A
  change Windows refuses shows a balloon ("Group policy may be managing it") instead of an error.
- `StartupRegistrar.EnsureRegisteredOnFirstRun` registers once. With no `AutoStartInitialized`
  value under `HKCU\Software\MindfulnessReminder`, it enables auto-start and writes `"1"` there,
  and the tray says so in a balloon. On every later launch it only calls `RepairPathIfNeeded`,
  which rewrites a Run value whose path differs, ignoring case, from the running executable's,
  registers nothing that is not registered, and "Never lifts a Task Manager block". If Windows
  refuses a write in either case (`UnauthorizedAccessException`, `SecurityException`), the
  refusal is caught: nothing is registered, no marker is written, and the next start tries
  again (the code review of 2026-10-09).

## Alternatives rejected

- **A registration that needs elevation.** The per-user Run key needs "No elevation" (README).
- **A checkbox that reads the Run key alone.** It "would report 'on' for an entry Windows
  ignores" (README).
- **Registering on every launch.** It would undo an untick at the next start.
- **Caching the checkbox state.** "the user can change auto-start behind our back from Task
  Manager's Startup tab" (`TrayIconHost`).

## Consequences

- Unticking the item sticks across restarts; ticking it again also lifts a Task Manager veto, "so
  one click always restores the intended state" (`TrayIconHost`).
- The Run value holds the path the application ran from when auto-start was switched on. After a
  move, "the first launch after a move has to happen manually", and that launch repairs the path;
  the README's deployment note says to publish somewhere stable.
- The marker shares its key with the settings (ADR 0027).
- Guards: `RegistryAutoStartManagerTests` (the quoted path; the approval flag's odd and even
  first byte; a veto with no Run value; `Enable` lifting a veto; the repairs, a veto kept through
  one) and `StartupRegistrarTests` (a first run registers and records the marker; a later run
  does not re-register after an untick, repairs a moved path and keeps a veto). The tray item and
  the balloons in `TrayIconHost`: none by test; launch the application from the tray.

Source: README.md ("Behaviour"; "Deployment note"; "Design decisions", rows "Auto-start" and
"Auto-start truth"); CLAUDE.md ("Structure");
src/MindfulnessReminder.Core/RegistryAutoStartManager.cs;
src/MindfulnessReminder.Core/StartupRegistrar.cs; src/MindfulnessReminder.Core/IAutoStartManager.cs;
src/MindfulnessReminder.Core/AutoStartStatus.cs; src/MindfulnessReminder.App/TrayIconHost.cs;
src/MindfulnessReminder.App/App.xaml.cs.
