# 0015. The coordinator is the single place that decides when a reminder appears: one debounce, a raise instead of a second window, nothing on a lock

Date: 2026-10-09
Status: accepted 2026-10-09

## Context

Retroactive: recorded on 2026-10-09 from `CLAUDE.md` ("Structure", the line on
`ReminderCoordinator.cs`), the summary of `src/MindfulnessReminder.Core/ReminderCoordinator.cs`
and the comment on `ISessionMonitor.Locked`; the decision itself dates from 61a0121 (the base
version, 2026-08-28), whose coordinator turned "unlock events into at most one reminder window",
and from b6554d5 (the periodic reminder, 1.1.0, 2026-09-08), which sent the periodic schedule and
the new lock event through the same class. The unlock side "is less trivial than it sounds":
"Windows can raise SessionUnlock twice for a single unlock", and "if a reminder from earlier is
still on screen, a second window would be noise" (the `ReminderCoordinator` summary). 1.1.0
added a second source of reminders, the schedule, which can fall due while the unlock reminder is
still open, and a lock event that the schedule needs and that must show nothing (ADR 0014).

## Decision

- Every reminder of the tray run goes through `ReminderCoordinator`. Its two sources are an
  unlock (`HandleUnlock`) and a due deadline of the schedule (`HandleTick`); only the coordinator
  calls the presenter. "Both sources of one (an unlock, and the periodic schedule) go through it,
  so the rules about not stacking windows are written once" (`CLAUDE.md`, "Structure").
- On either path a reminder already on screen, or queued to appear, is raised with
  `BringToFront` instead of a second window being opened.
- An unlock less than `ReminderCoordinator.DefaultDebounce` (2 s) after the last reminder shows
  nothing: the unlock path records the time when it shows one, the periodic path whenever it
  serves a due deadline, and the unlock path checks that time.
- A lock shows nothing: `HandleLock` suspends the schedule (ADR 0014) and touches no window.
  "Nothing shows a reminder on lock" (the comment on `ISessionMonitor.Locked`; ADR 0029).
- The rules added later went into the same class: the unlock reminder's own switch (ADR 0021),
  holding a reminder back (ADR 0018) and the choice of message (ADR 0022).

## Alternatives rejected

- **Each source deciding on its own window.** The rules about stacking would be written once per
  source; the coordinator exists so that they are "written once" (`CLAUDE.md`, "Structure").
- **A second window over one already open.** "a second window would be noise -- the existing one
  is raised instead" (the `ReminderCoordinator` summary).
- **No debounce.** "Windows can raise SessionUnlock twice for a single unlock, so bursts are
  collapsed by a short debounce window" (the same summary).

## Consequences

- A new reason to show a reminder is a new path in `ReminderCoordinator`, with its tests in
  `ReminderCoordinatorTests`, not a second caller of the presenter.
- `--preview` is the one window outside it: it opens a `BreakWindow` directly, in a run with no
  tray icon, no session hook and no timer.
- The presenter checks as well, in the App: `WpfReminderPresenter.IsShowing` counts a reminder
  still in its 600 ms show delay as showing, "so a burst cannot queue two windows", and its
  `Show` raises the window already there instead of queueing another (ADR 0026). That check has
  no test.
- The debounce is the unlock's only: a due deadline does not consult it.
- Guards: `ReminderCoordinatorTests` (a duplicate unlock within the debounce is ignored and a
  later one shows the reminder again; an unlock just after a periodic reminder is debounced; an
  unlock while a reminder is open raises it instead of opening a second; a due periodic reminder
  over an open one raises it; a lock never shows a reminder by itself). The presenter's own check
  is App: none by test; launch the application from the tray.

Source: CLAUDE.md ("Structure"; "The periodic reminder"; "Build & test");
src/MindfulnessReminder.Core/ReminderCoordinator.cs;
src/MindfulnessReminder.Core/ISessionMonitor.cs;
src/MindfulnessReminder.Core/IReminderPresenter.cs;
src/MindfulnessReminder.App/WpfReminderPresenter.cs; src/MindfulnessReminder.App/App.xaml.cs;
tests/MindfulnessReminder.Tests/ReminderCoordinatorTests.cs; the diffs of 61a0121 and b6554d5.
