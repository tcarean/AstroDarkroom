# 0012. One timer, ticking every second, drives a deadline; no timer is ever armed for the interval

Date: 2026-10-09
Status: accepted 2026-10-09

## Context

Retroactive: recorded on 2026-10-09 from `CLAUDE.md` ("The periodic reminder", the first
decision), `README.md` ("Periodic reminders", the first bullet; "Design decisions", row
"Periodic schedule"), the summaries of `IPeriodicTimer` and `ReminderCoordinator` and the comment
on `ReminderCoordinator.DefaultTickInterval`; the decision itself dates from b6554d5 (the periodic
reminder, 1.1.0, 2026-09-08). Until then the application only reacted to an unlock. 1.1.0 added a
reminder every N minutes while the user works (30 by default, 1 to 480) and a countdown to it in
the tray tooltip, so two things needed a tick: deciding whether the interval has elapsed, and
redrawing the countdown. "The requirement was explicitly for one timer" (`CLAUDE.md`), and the
countdown had to restart on every unlock (ADR 0014).

## Decision

- The periodic reminder has one timer, `IPeriodicTimer`, ticking at
  `ReminderCoordinator.DefaultTickInterval` (1 s): "One second is not the reminder interval -- it
  is the resolution of the countdown in the tray tooltip, which is what has to look alive" (the
  comment on it).
- `IntervalSchedule` holds the deadline. On each tick `ReminderCoordinator.HandleTick` asks it
  `TryConsumeDue(now)`; on a yes a reminder is shown, the open one raised (ADR 0015) or the
  reminder held back (ADR 0018). Every tick raises `CountdownChanged`, which the App turns into
  the tooltip.
- In production the timer is `DispatcherPeriodicTimer`, a `DispatcherTimer` in the App, so its
  ticks arrive on the UI thread, where the window is created and the tooltip written. Starting it
  while it runs does not restart its period.
- The timer runs only while the schedule counts down: `SyncTimer` stops it while the workstation
  is locked and while the periodic reminder is off.

## Alternatives rejected

- **A one-shot timer armed for the interval.** It "would have needed a second timer beside it" to
  redraw the countdown (`CLAUDE.md`, "The periodic reminder"), and restarting the countdown on an
  unlock would have been "a timer torn down and rebuilt" instead of "one line" (the
  `ReminderCoordinator` summary).

## Consequences

- Restarting the countdown, on an unlock, after a reminder or when Settings are saved, sets a new
  deadline (`IntervalSchedule.Restart`, ADR 0014); no timer is torn down.
- `CountdownChanged` fires every second; `App.UpdateTooltip` writes the tooltip only when its text
  changed, "1 write in 60" (ADR 0025). Nothing costly runs on every tick: the activity monitor
  is read when a reminder is due, once per retry, and otherwise every 10 s (ADR 0019).
- The timer keeps ticking while the modeless Settings window is open (the
  `DispatcherPeriodicTimer` summary; ADR 0026).
- "One timer" means the periodic one. `WpfReminderPresenter` has its own one-shot
  `DispatcherTimer` for the 600 ms show delay (ADR 0026), as it has had since 61a0121 (the base
  version, 2026-08-28), and `--detect <seconds>` runs a `DispatcherPeriodicTimer` of its own, in a
  run that has no schedule. The `IPeriodicTimer` summary still calls the periodic timer "The one
  timer the whole app owns".
- The tests drive the schedule with a `FakeTimer` that "Ticks only when the test says so, so half
  an hour costs nothing".
- Guards: `ReminderCoordinatorTests` (the countdown starts with the application; nothing before
  the interval, a reminder once it has passed and every interval after; `CountdownChanged` on
  every tick and every reset; no timer while the periodic reminder is off or the workstation is
  locked) and `IntervalScheduleTests` (due exactly at the interval and not before; re-armed for a
  whole interval). `DispatcherPeriodicTimer` is App: none by test; launch the application from the
  tray and watch the tooltip count down.

Source: CLAUDE.md ("The periodic reminder"); README.md ("Periodic reminders"; "Design decisions",
row "Periodic schedule"); src/MindfulnessReminder.Core/IPeriodicTimer.cs;
src/MindfulnessReminder.Core/ReminderCoordinator.cs;
src/MindfulnessReminder.Core/IntervalSchedule.cs;
src/MindfulnessReminder.App/DispatcherPeriodicTimer.cs;
src/MindfulnessReminder.App/WpfReminderPresenter.cs; src/MindfulnessReminder.App/App.xaml.cs;
tests/MindfulnessReminder.Tests/ReminderCoordinatorTests.cs.
