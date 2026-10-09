# 0033. One instance per session through a named mutex, no main window and OnExplicitShutdown, a crash dialog with the version; --preview and --detect run before the mutex

Date: 2026-10-09
Status: accepted 2026-10-09

## Context

Retroactive: recorded on 2026-10-09 from `CLAUDE.md` ("Structure", the `App.xaml.cs` line),
`README.md` ("Checking it on your machine", the mutex sentence; "Versioning", the row "crash
dialog") and the comments of `App.xaml.cs`; the decision itself dates from 61a0121 (the base
version, 2026-08-28: the mutex, the shutdown mode, the crash dialog, and `--preview` ahead of the
mutex), with the banner added to the crash dialog by d3ccd6c (licensing and versioning, 1.0.0,
2026-09-01) and `--detect` placed ahead of the mutex by 89de6c8 (pausing for a call or full
screen, 1.2.0, 2026-09-08). The application has no window to live in: it is a tray icon, a
reminder that comes and goes, and windows the user opens. Windows can start it twice: "Windows
can hand us a second launch (Run key plus a manual start); the one already in the tray owns the
session" (`App.OnStartup`). And a tray application that crashes leaves nothing behind: "A tray
app that dies silently just vanishes".

## Decision

- `App.OnStartup` looks for `--preview`, then for `--detect`, and runs either and returns before
  anything else. Otherwise it claims the mutex `Local\MindfulnessReminder.SingleInstance`,
  "Per-session, so a second desktop session gets its own instance". A second copy disposes the
  mutex and shuts down without a word; the first releases it in `OnExit`.
- `App.xaml` sets `ShutdownMode="OnExplicitShutdown"`, and there is no main window: "otherwise
  closing the reminder would quit". Closing a window ends nothing; the application calls
  `Shutdown()` itself, on Exit in the tray menu, when the `--preview` window closes, after the
  `--detect` report, in a second copy, and from the crash handler.
- The crash handler, `DispatcherUnhandledException` and, for an error in a tray-menu action,
  Windows Forms' `Application.ThreadException`, attached once the mutex is held, shows a
  `MessageBox` titled "Mindfulness Reminder stopped" with the error's message and then
  `VersionInfo.Banner`, marks the exception handled and shuts down. The banner is there because
  "a screenshot of this dialog is often all a bug report has, and it has to say which version
  broke".
- `--preview` and `--detect` never claim the mutex, so both run "alongside the copy already in
  the tray" (README; ADR 0020 for `--detect`).

## Alternatives rejected

- **WPF's default shutdown mode.** With no main window, "closing the reminder would quit" (the
  `App` class summary).
- **One instance for the whole machine.** The `Local\` name is per session, so that "a second
  desktop session gets its own instance".
- **Dying without a word.** "A tray app that dies silently just vanishes; say so before going."
- **The two switches behind the mutex.** Neither would run while the tray copy does, and the
  point of `--detect` "is to watch what the copy already sitting in the tray would be seeing, so
  it has to run alongside it" (`App.ShowDetection`).

## Consequences

- Every way out calls `Shutdown()`; a new switch that shows a window shuts down on its `Closed`,
  as `--preview` does.
- A switch placed ahead of the mutex runs beside the tray copy; `--preview` touches no registry,
  and `--detect` only reads it.
- The crash handler covers the tray copy only: `--preview` and `--detect` return before it is
  attached.
- The crash dialog stays a `MessageBox` (ADR 0032), and the banner it shows comes from the one
  version source (ADR 0011).
- Guards: none by test; launch the application from the tray, start it a second time (the second
  copy exits with nothing on screen), close a reminder (the tray icon stays), and run `--preview`
  or `--detect` beside it.

Source: CLAUDE.md ("Structure"); README.md ("Checking it on your machine"; "Versioning");
src/MindfulnessReminder.App/App.xaml; src/MindfulnessReminder.App/App.xaml.cs.
