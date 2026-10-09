# 0020. --detect shows the raw readings and exits; with a duration it samples once a second and renders a timeline through DetectionLog, in Core

Date: 2026-10-09
Status: accepted 2026-10-09

## Context

Retroactive: recorded on 2026-10-09 from `CLAUDE.md` ("Pausing for what the user is doing", its
last two paragraphs; "Build & test", the two `--detect` commands), `README.md` ("Checking it on
your machine"), the comments on `DetectSwitch`, `MaxDetectDuration`, `DetectDuration` and
`ShowDetection` in `App.xaml.cs`, and `DetectionLog.cs`; the decision itself dates from 89de6c8
(pausing for a call or full screen, 1.2.0, 2026-09-08). Neither pause signal "can be exercised
without a real call or a real game" (`CLAUDE.md`), and only the raw readings tell "a signal that
is not arriving from a setting that is switched off". The
application is a `WinExe`, with "no console to print on" (the `ShowDetection` comment). For full
screen, "a reading taken as the switch runs is useless there, because getting to a terminal to
run it means leaving full screen and destroying the state being measured" (`CLAUDE.md`).

## Decision

- `--detect` takes one reading, shows it in a message box and exits: which application holds
  the microphone or the camera, the raw number `SHQueryUserNotificationState` returned and what
  it was read as (`WindowsActivityMonitor.Describe`), and the verdict of `SuppressionPolicy`
  under the saved settings.
- `--detect <seconds>`, or `--detect=<seconds>`, takes one reading a second, on a
  `DispatcherPeriodicTimer` at `ReminderCoordinator.DefaultTickInterval`, for that long, and
  shows the message box only when the run is over: `DetectionLog.Render` of the samples, one
  line per run of identical readings, timed in minutes and seconds from the start of the run,
  then how many of the readings would have held a reminder back.
- `MaxDetectDuration` clamps the duration to ten minutes, "Long enough for any test, short
  enough that a typo ends"; a duration that does not parse, or is not positive, falls back to
  the single reading.
- `DetectionLog` is in Core and tested; `App.xaml.cs` only samples and shows.
- Neither `--detect` nor `--preview` claims the single-instance mutex: both are handled at the
  top of `OnStartup`, before it is claimed, so they run beside the copy already in the tray
  (ADR 0033).

## Alternatives rejected

- **The single reading alone.** It does for a call, and is useless for full screen, which
  running the switch destroys.
- **One line per reading.** "Thirty seconds of sampling is thirty lines that all say the same
  thing, and the two or three moments where the answer CHANGED are the entire content of the
  report" (the `DetectionLog` summary).
- **Wall-clock times on the timeline.** The reader lines it up against what they were doing,
  "and they were counting from when they started the switch" (the comment on
  `DetectionLog.Clock`).
- **The timeline rendered, untested, in the App.** It is in Core and tested because "a report
  that quietly lost the one moment the reading changed would send someone hunting a bug that is
  not there" (`CLAUDE.md`).
- **Refusing a mistyped duration.** Anything unparseable is a zero, because "a diagnostic that
  refuses to run because its argument was mistyped is no help to anybody" (the `DetectDuration`
  comment).
- **Claiming the mutex like a normal launch.** With the tray copy running it would exit at once
  (ADR 0033), and "the point is to watch what the copy already sitting in the tray would be
  seeing, so it has to run alongside it" (the `ShowDetection` comment).

## Consequences

- The full-screen check is to start `--detect 60`, "launch the game, play for a while, quit, and
  read what it saw" (README). The message box waits for the end of the run, "so it cannot
  disturb what is being measured".
- `--detect` reads the saved settings for its verdict and writes nothing; `--preview` does not
  touch the registry at all.
- The box is a `MessageBox`, light, since the application exits straight after it (ADR 0032).
- Guards: `DetectionLogTests` (no readings, identical readings collapsed into one line, a change
  starting a new line, a reading that returns as a second run, what each line says, the count of
  held-back readings, the sentence for a run that held nothing back, runs over a minute). The
  switch, its parsing and clamp, the sampling and the message box are in `App.xaml.cs`, with no
  test: launch the application with `--detect` and with `--detect 60`.

Source: CLAUDE.md ("Pausing for what the user is doing", "Build & test"); README.md ("Checking
it on your machine"); src/MindfulnessReminder.App/App.xaml.cs (`DetectSwitch`,
`MaxDetectDuration`, `DetectDuration`, `ShowDetection`, `Report`);
src/MindfulnessReminder.App/WindowsActivityMonitor.cs (`Describe`);
src/MindfulnessReminder.Core/DetectionLog.cs.
