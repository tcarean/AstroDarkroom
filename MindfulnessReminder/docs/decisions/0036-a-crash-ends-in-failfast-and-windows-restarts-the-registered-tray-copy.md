# 0036. A crash ends in FailFast, and Windows restarts the registered tray copy

Date: 2026-10-26
Status: accepted 2026-10-26

## Context

On 2026-10-26 the tray copy, started at 07:47 on .NET 8.0.32, lost its runtime: a Visual Studio
security update delivered by Windows Update uninstalled the 8.0.32 runtimes at 07:56 and installed
8.0.33. Files already mapped stayed; every file not yet loaded was gone. At 08:17 the first
reminder window needed `UIAutomationTypes.dll`, and the crash dialog of R-076 appeared, with an
empty message (`mscorrc.dll`, the runtime's resource library, was gone too). On `OK` at 08:21 the
handler called `Application.Shutdown`, WPF's shutdown ran `ControlsTraceLogger.LogUsedControlsDetails`,
which needed `System.Diagnostics.Tracing.dll`, and the process died inside its own crash handler,
with no restart and no `.NET Runtime` event. Both failures were reproduced to the same WER bucket
(2109401837353861155) by running the build against a copy of the runtime trimmed to the files the
crashed process had loaded. Restart Manager had tried to restart the application at 07:54 and
could not, the installer running as SYSTEM and the application as the user ("Application SID does
not match Conductor SID", event 10010).

The application is framework-dependent (ADR 0002) and lives in the tray for weeks; any .NET or
Visual Studio update can take its runtime away. What was measured for this decision, with a probe
registering `RegisterApplicationRestart("--restarted", RESTART_NO_REBOOT)` and ending in
`Environment.FailFast`: after 65 s of running, Windows started the copy again 0.2 s after the exit,
with the argument; after 20 s, it did not. `FailFast` writes a `.NET Runtime` event 1025 with the
message and the stack, and Windows Error Reporting treats it as a crash (bucket type CLR20r3).

## Decision

The tray copy registers for automatic restart right after it claims the mutex, with `--restarted`
as the command line and `RESTART_NO_REBOOT`; the crash dialog shows `CrashReport.Format` (type,
message, the missing file's name, the inner exceptions) and then hides the tray icon and ends the
process through `Environment.FailFast` with that text and the exception, never through
`Application.Shutdown`. A copy started with `--restarted` says so in a balloon. What is decided
sits in Core (`ApplicationRestart`, `CrashReport`); the two Windows calls and the balloon are in
App (R-076, R-100 to R-102).

## Alternatives rejected

- **`Environment.Exit` after the dialog.** It avoids WPF's shutdown path too, but a clean exit is
  not a crash to Windows, so a registered application is not restarted; the one thing the owner
  wanted after 2026-10-26, the application back in the tray, would not happen.
- **The handler starts a new copy itself.** `Process.Start` needs `System.Diagnostics.Process`,
  never loaded in the tray copy, so it would have failed on 2026-10-26 like the window did; a
  `CreateProcess` P/Invoke would work, but then the application owns the loop guard Windows
  already provides with its 60-second rule.
- **Self-contained publish.** It removes the exposure to runtime updates entirely, but it
  contradicts ADR 0002, adds about 70 MB per release, and leaves the crash handler as fragile as
  it was.
- **Restart after a reboot as well (flags 0).** The Run entry already starts the application at
  logon (R-003); a second start would only exit on the mutex (R-002).
- **Nothing in code, a restart by hand after updates.** The failure was silent in effect: a dialog
  with no message, then nothing in the tray for the rest of the day.

## Consequences

- A crash of the tray copy that has run for a minute ends with the application back in the tray on
  whatever runtime is installed, and the Application log holds the text and the stack (event 1025),
  which the dialog alone never did. A crash within the first minute is not restarted, by Windows'
  rule, so a copy broken at start cannot loop.
- The dialog's text is a pure function, `CrashReport.Format`, tested in Core (R-101); the switches
  that decide registration and the restart balloon are tested in Core too (R-100, R-102). The two
  Windows calls stay in App and are checked only by a tray start, which the owner does in their own
  copy after the merge (ADR 0008).
- `FailFast` skips `OnExit`: the mutex is released by the kernel as abandoned, and the tray icon
  is hidden by the handler before the fail-fast, so nothing is left behind. The dump Windows writes
  lands where Windows Error Reporting is configured, on the owner's machine `%LOCALAPPDATA%\CrashDumps`.
- The registration costs one call at start and nothing after; a refused registration is ignored,
  the application running as before 2026-10-26.
