# 0022. The coordinator picks the text and the presenter draws it; BringToFront takes nothing, so a window keeps the text it opened with

Date: 2026-10-09
Status: accepted 2026-10-09

## Context

Retroactive: recorded on 2026-10-09 from `CLAUDE.md` ("Two reminders, two switches, two
messages", the paragraph "The coordinator picks the text, the presenter draws it"; "Structure",
the line on `BreakWindow.xaml(.cs)`), the comments of `IReminderPresenter.cs`,
`WpfReminderPresenter.Show`, the `BreakWindow` constructor and `ReminderCoordinator.HandleTick`,
and the diff of 2be5361; the decision itself dates from 2be5361 (a switch and a message for each
reminder, 1.4.0, 2026-09-14). Until then `IReminderPresenter.Show()` took nothing, and the one
text was written in `BreakWindow.xaml` ("Ground yourself" since f3c8cba, the changed message,
2026-09-08). With a message per reminder (ADR 0021),
something has to know which reminder a window is for, and the two can fall due over each other:
a periodic deadline can pass while the unlock reminder is on screen, or still in the 600 ms
`WpfReminderPresenter` waits before it creates the window (ADR 0026), and an unlock can come
while a periodic reminder is open.

## Decision

- `IReminderPresenter.Show(string message)` takes the text. The coordinator passes
  `Settings.UnlockMessage` from `HandleUnlock` and `Settings.PeriodicMessage` from
  `HandleTick`, "because the coordinator is the one thing that knows which reminder this is"
  (`CLAUDE.md`). The presenter "only draws what it is handed" (the `IReminderPresenter`
  comment), and `BreakWindow` takes the message in its constructor and has no text of its own.
- `BringToFront()` takes nothing. A reminder falling due while one is showing raises that one as
  it is: the coordinator calls `BringToFront` when `IsShowing`, and `WpfReminderPresenter.Show`
  does the same when it is called with a window open or queued. A window already open, or still
  in its 600 ms queue, keeps the text it opened with.

## Alternatives rejected

- **The window or the presenter choosing the text.** Neither knows which reminder it is showing;
  the window "has no idea, and does not need one" (the comment on the `BreakWindow`
  constructor).
- **Raising an open window with the new reminder's text.** The text would be "swapped under the
  user's eyes when the other reminder falls due over it" (`CLAUDE.md`), and the deadline is
  served either way: "A reminder already on screen means the moment has been served" (the
  comment in `HandleTick`).

## Consequences

- Messages saved in Settings take effect on the next reminder, never on one already open or
  queued.
- Guards: `ReminderCoordinatorTests` (the unlock reminder says the unlock message, the periodic
  one the periodic message, a periodic reminder due over the unlock one raises it without
  changing its text, new messages take effect on the next reminder). The queue that keeps its
  message is in `WpfReminderPresenter`, with no test: launch the application with `--preview`,
  or from the tray.

Source: CLAUDE.md ("Two reminders, two switches, two messages", "Structure");
src/MindfulnessReminder.Core/IReminderPresenter.cs;
src/MindfulnessReminder.Core/ReminderCoordinator.cs (`HandleUnlock`, `HandleTick`);
src/MindfulnessReminder.App/WpfReminderPresenter.cs;
src/MindfulnessReminder.App/BreakWindow.xaml.cs; src/MindfulnessReminder.App/BreakWindow.xaml; the
diff of 2be5361.
