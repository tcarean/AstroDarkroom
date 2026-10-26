# Pausing for what the user is doing: the device, full screen, --detect

> Moved unchanged on 2026-10-09 (ADR 0001) from `CLAUDE.md`, "Pausing for what the user is doing".
> Where a paragraph became a decision record, its number is given after it. **Covers:**
> `src/MindfulnessReminder.App/WindowsActivityMonitor.cs`,
> `src/MindfulnessReminder.Core/ForegroundState.cs`,
> `src/MindfulnessReminder.Core/SuppressionPolicy.cs`,
> `src/MindfulnessReminder.Core/IActivityMonitor.cs`,
> `src/MindfulnessReminder.Core/ReminderCoordinator.cs`,
> `src/MindfulnessReminder.Core/IntervalSchedule.cs`,
> `src/MindfulnessReminder.Core/DetectionLog.cs`, `src/MindfulnessReminder.App/App.xaml.cs`. A
> change to the code this describes means this note is updated in the same task, or deleted with the
> decision it explains.

## Pausing for what the user is doing

Added in 1.2.0. Two switches, both on by default, that hold a reminder back rather than put
it over the top of something. They are **independent of each other and of the periodic
reminder** -- the unlock reminder obeys them too, which is why they sit at the top level of
the Settings window rather than inside the periodic panel.

**Reading the device, not the window title, is the whole point.** Windows records per-app
capture usage under `HKCU\...\CapabilityAccessManager\ConsentStore\{microphone,webcam}`,
(and the same path under `HKLM`, which the adapter also reads: ADR 0016),
with `LastUsedTimeStop == 0` while the stream is open -- the same record its own tray privacy
indicator reads. That covers Zoom, Teams (desktop and browser), a Slack huddle, Meet, Webex,
Discord, and whatever ships next, and it is still right on mute. A table of window titles was
considered and rejected: it needs an entry per application, breaks on each new release, and
misses a Slack huddle outright, because a huddle lives inside the main Slack window and has
no title of its own. **Do not reintroduce one.**

See ADR 0016.

Full screen is `SHQueryUserNotificationState`, one P/Invoke, mapped in Core by
`UserNotificationState.FromShellState` so the table is testable. If it ever proves lossy on
borderless-fullscreen windows, the fallback is `GetForegroundWindow` + `GetWindowRect` against
the monitor bounds -- `--detect` is how to find out.

See ADR 0017.

Rules that are easy to break:

- **`QUNS_NOT_PRESENT` must not suppress.** It is what Windows reports behind a lock screen
  and can still be the answer for a moment after an unlock -- long enough to swallow the
  unlock reminder. Nothing is lost by ignoring it: the schedule is suspended across a lock
  anyway.
- **A periodic reminder is POSTPONED; an unlock reminder is SKIPPED.** The unlock already
  restarted the schedule, so a reminder is coming; a one-shot retry beside the periodic one
  would be a second deadline to keep in step with the first.
- **`IntervalSchedule` compares the backwards-clock guard against `_armedSpan`, not against
  the interval.** A retry gap longer than the interval -- 300 s against a 1-minute interval --
  would otherwise look like a clock jump, reset the schedule and throw the postponement away.
  `R025_A_postponement_longer_than_the_interval_is_not_read_as_a_clock_jump` pins it.
- **The activity monitor is read when a reminder is due, once per retry, and otherwise once
  every `DefaultActivityRefresh` (10 s) to keep the tooltip current.** Never on the
  one-second tick: that would be 1800 registry walks per reminder. The idle refresh is not
  decoration -- without it `Suppression` only becomes non-None at the moment a reminder is
  held back, so hovering the tray during a call would count down to a reminder that is not
  going to arrive, and the whole feature would be invisible until the interval happened to
  elapse. That is exactly how it was first written, and it was wrong.
- **No cap on how long a reminder is held back.** That is the requirement, and the tooltip
  carries the reason so a quiet tray icon can be told from a broken one.

See ADR 0017 for the first rule, ADR 0018 for the second and the last, ADR 0013 for the third
and ADR 0019 for the fourth.

The decision log records a skipped unlock reminder with its reason, and a held-back periodic
one with one line when the hold starts, one more only when the reason changes, and one with the
total when it finally appears, or is raised because its window was already open: the coordinator
keeps the moment the hold began (`_heldSince`) for that line alone, and clears it on a lock, an
unlock, a settings change, or a clock moved back, which restart the countdown anyway
(`IntervalSchedule.JumpRestartedCountdown` tells it about the last of those). The retries in
between, and the idle 10-second readings, write nothing (ADR 0037).

`--detect` shows the raw readings and exits. It exists because neither signal can be
exercised without a real call or a real game, and it is the only way to tell a signal that is
not arriving from a setting that is switched off.

See ADR 0020.

**`--detect <seconds>` is the form that matters for full screen**, and the reason it exists is
not convenience: a reading taken as the switch runs is useless there, because getting to a
terminal to run it means leaving full screen and destroying the state being measured. With a
duration it samples once a second and renders a timeline through `DetectionLog` -- which is in
Core, and tested, because a report that quietly lost the one moment the reading changed would
send someone hunting a bug that is not there. The message box is shown only after the run
ends, so it cannot disturb what is being measured.

See ADR 0020.
