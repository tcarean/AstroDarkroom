# 0014. A lock suspends the countdown and every unlock restarts it in full, before anything decides on a window; a due deadline re-arms at once; saving settings restarts it

Date: 2026-10-09
Status: accepted 2026-10-09

## Context

Retroactive: recorded on 2026-10-09 from `CLAUDE.md` ("The periodic reminder", the third
decision, the two smaller rules and the paragraph on `ApplySettings`), `README.md` ("Periodic
reminders", the opening paragraph and the third bullet) and the comments on `Suspend`, `Resume`,
`Apply`, `Restart` and `TryConsumeDue` in `src/MindfulnessReminder.Core/IntervalSchedule.cs` and
on `HandleUnlock` and `ApplySettings` in `ReminderCoordinator.cs`; the decision itself dates from
b6554d5 (the periodic reminder, 1.1.0, 2026-09-08). The requirement called out the lock by name
(the summary of the three-hour lock test in `IntervalScheduleTests`): with 10 minutes left, a
lock, three hours away and an unlock, "the next reminder is 30 minutes after that unlock — for
any number of lock/unlock cycles, of any length" (README, "Periodic reminders"). Without a
suspend, "the deadline would pass behind the lock screen, the window would be created where
nobody can see it, and it would then already be open when the unlock reminder tried to appear, so
the coordinator would raise it instead of showing one" (`CLAUDE.md`). The comment on `Restart`
calls it "the only place a deadline is ever set"; since 89de6c8 (pausing for a call or full
screen, 1.2.0, 2026-09-08) `Postpone` sets one too, the retry gap of a held-back reminder
(ADR 0018).

## Decision

- A lock suspends the schedule: `HandleLock` calls `IntervalSchedule.Suspend`, which drops the
  deadline, and the timer stops. Nothing is counted behind a lock screen, and nothing is shown
  there (ADR 0015).
- Every unlock restarts the countdown in full: `HandleUnlock` calls `Resume`, which "grants the
  whole interval afresh regardless of how long the lock lasted" (`CLAUDE.md`), so no time is
  carried across a lock; an unlock with no lock before it restarts the countdown too.
- The restart is the first thing `HandleUnlock` does, before the unlock reminder's own switch
  (ADR 0021), the already-showing check and the debounce: "Those two decide whether a *window*
  appears; the schedule is not theirs to skip" (`CLAUDE.md`).
- `TryConsumeDue` re-arms a whole interval as it returns true, so a due reminder that cannot be
  shown, because one is already on screen, "still leaves a sane schedule" (`CLAUDE.md`).
- `ApplySettings` restarts the countdown from zero under the new settings
  (`IntervalSchedule.Apply`).
- So the whole interval goes back on the clock in one place, `Restart`: at startup, on every
  unlock, after each due deadline, when settings are applied, and after a backwards clock jump
  (ADR 0013).

## Alternatives rejected

- **Counting on through a lock.** The deadline would pass behind the lock screen, and the
  reminder created there would already be open when the unlock came, "swallowing the unlock
  reminder that follows" (the comment on `Suspend`).
- **Carrying the time left across a lock.** The requirement is a whole interval after every
  unlock, which "is why no time is carried across a lock" (`CLAUDE.md`): 10 minutes left before a
  three-hour lock still means 30 after the unlock.
- **The restart after the debounce or the already-showing check.** "letting either of them skip
  this would leave a deadline set before a lock that may have lasted hours" (the comment in
  `HandleUnlock`).
- **Re-arming only when a window opens.** With a reminder already on screen, "every subsequent
  tick would find a deadline still in the past and hammer BringToFront" (the summary of the
  re-arm test in `ReminderCoordinatorTests`).
- **Keeping the elapsed time when settings change.** It "would mean changing 30 minutes to 10
  with 25 gone either fires immediately or leaves the tooltip showing a number the new setting
  cannot explain" (`CLAUDE.md`).

## Consequences

- A held-back periodic reminder replaces the interval `TryConsumeDue` re-armed with the retry
  gap, through `Postpone` (ADR 0018); the next restart puts the whole interval back.
- With the unlock reminder switched off an unlock still restarts the countdown, because the
  restart belongs to the periodic reminder (ADR 0021).
- While the workstation is locked there is no countdown: `Remaining` is null and the tooltip is
  the banner alone (ADR 0025).
- The Settings window says it under the interval, "The countdown restarts when you save.", and
  the README says it in "Periodic reminders".
- Guards: `IntervalScheduleTests` (a lock stops the countdown; a lock longer than the interval,
  and any number of lock and unlock cycles, leave a whole interval after the last unlock; an
  unlock without a lock restarts it; an unlock does not start a disabled schedule; applying
  settings restarts from zero; a due deadline re-arms for a whole interval) and
  `ReminderCoordinatorTests` (an unlock restarts the countdown; the three-hour lock end to end;
  many cycles; a lock stops the timer and clears the countdown; a due reminder over an open one
  raises it and still re-arms; applying settings restarts from zero; with the unlock reminder off
  an unlock still restarts the countdown).

Source: CLAUDE.md ("The periodic reminder"); README.md ("Periodic reminders");
src/MindfulnessReminder.Core/IntervalSchedule.cs;
src/MindfulnessReminder.Core/ReminderCoordinator.cs;
src/MindfulnessReminder.App/SettingsWindow.xaml;
tests/MindfulnessReminder.Tests/IntervalScheduleTests.cs;
tests/MindfulnessReminder.Tests/ReminderCoordinatorTests.cs.
