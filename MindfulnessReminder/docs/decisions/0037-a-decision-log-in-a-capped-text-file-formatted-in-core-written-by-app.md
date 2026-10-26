# 0037. A decision log in a capped text file, formatted in Core, written by App

Date: 2026-10-26
Status: accepted 2026-10-26

## Context

The 2026-10-26 crash was diagnosed from outside the application: the Event Viewer gave the
process start time and the installer's steps, a WER mini dump gave the HRESULT and the loaded
modules, and a reproduction gave the stack. The application itself had recorded nothing: not
which runtime it started on, not that the restart registration had been made (R-100 ignores
its HRESULT), not that a reminder was due at 08:17 and what it decided. Since ADR 0036 a crash
leaves its text and stack in the Application log; what still leaves no trace is everything the
application decides without failing: a reminder skipped at unlock during a call (R-014) or held
back for full screen (R-033), a lock or unlock that never arrived, a setting that fell back to
its default (R-051), an auto-start entry repaired (R-006). Those are the likely complaints about
a reminder ("it did not appear"), and `--detect` answers them only live, at the keyboard.

The volume is small by construction: a start, a few locks and unlocks a day, one line per
reminder, one per change of a held-back reason, never one per tick or per retry. A day is some
tens of lines; 256 KB holds months.

## Decision

The tray copy keeps a decision log: `log.txt` in `%LOCALAPPDATA%\MindfulnessReminder\`, one
line per event with the local time, capped at 262 144 bytes by one rotation to `log.1.txt`
(R-110). Core decides what is an event and renders every line (`DecisionLog`, `IDecisionLog`
as the seam, raised from `ReminderCoordinator`, `RegistrySettingsStore`, `StartupRegistrar` and
the crash path: R-111 to R-114); App has one adapter, `FileDecisionLog`, which appends, rotates
and swallows every I/O error. The log is always on; the file is read in a text editor, and
nothing in the tray menu or About points at it.

## Alternatives rejected

- **The Windows Application log, under the application's own source.** Registering an event
  source needs administrator rights (`EventLog.CreateEventSource`), which a tray app of one user
  does not have, and an unregistered source is refused or lands under a misleading name. The
  crash already reaches that log through the fail-fast (ADR 0036); the decisions do not need to.
- **An `EventSource` (ETW) and traces collected on demand.** Right for a service under
  observation, wrong for a tray app whose owner opens a file after the fact; nothing would be
  collected unless a session was running at the time.
- **A log that is off by default, switched on in Settings.** The one time the log is needed is
  the day nobody knew it would be; a switch that was off then is a log that does not exist. The
  cost of always on is a file of a few hundred kilobytes.
- **One line per retry while held back.** A long call at a 20-second retry would write 180 lines
  an hour that all say the same thing; the reason's change is the event, so that is the line.
- **A tray-menu item or an About line that opens the log.** It adds a menu item to a four-item
  menu for a file the owner opens a few times a year; the path is in the README.
- **The timestamp added in Core from `IClock`.** The settings store and the registrar have no
  clock, and UTC on a machine the owner reads in local time would need converting every time;
  the adapter stamps local time at the write, through one Core function (`FormatLine`) that the
  tests pin.
- **The messages left out of the settings summary.** The point of the line is to see exactly
  what the application read; the two texts are the user's own, in the user's own file.

## Consequences

- A complaint about a reminder can be answered from the file: when the unlock arrived, what the
  coordinator decided and why, what the settings were. The start line says which runtime a copy
  ran on and whether Windows will restart it, two facts the 2026-10-26 diagnosis had to dig out.
- Every line is a pure function or a decision on fakes, tested in Core (R-110 to R-114);
  `FileDecisionLog` is the one piece checked only by a tray start (ADR 0008).
- `ReminderCoordinator`, `RegistrySettingsStore` and `StartupRegistrar` take an optional
  `IDecisionLog` and default to a null log, so no existing caller or test changes;
  `IAutoStartManager` gains `Command`, which `RegistryAutoStartManager` already had.
- `RestartRegistration.Register` returns the HRESULT instead of ignoring it, for the start line
  alone (R-100 still ignores a refusal).
- The log holds the user's two reminder messages and the paths of the executable; it is in the
  user's own profile folder and nowhere else.
