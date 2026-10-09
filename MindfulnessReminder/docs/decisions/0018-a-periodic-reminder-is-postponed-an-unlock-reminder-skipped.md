# 0018. A held-back periodic reminder is postponed by the retry gap, with no cap; a held-back unlock reminder is skipped; the tooltip gives the reason

Date: 2026-10-09
Status: accepted 2026-10-09

## Context

Retroactive: recorded on 2026-10-09 from `CLAUDE.md` ("Pausing for what the user is doing", the
rules "A periodic reminder is POSTPONED; an unlock reminder is SKIPPED" and "No cap on how long
a reminder is held back"), `README.md` ("Pausing for what you are doing", "What it will and will
not catch", the "Design decisions" row "Held-back reminder"), the summary of
`ReminderCoordinator` and the comments in `HandleTick`, `HandleUnlock` and
`SuppressionPolicy.Evaluate`; the decision itself dates from 89de6c8 (pausing for a call or full
screen, 1.2.0, 2026-09-08). Both reminders obey the two pauses (ADR 0016, ADR 0017), but they
arrive differently: the periodic one when a deadline passes (ADR 0012), the unlock one on an
event that has, on its way past, already restarted that deadline for a whole interval (ADR
0014). Anything holding the microphone open counts, so a reminder can be held back for a long
time, and a tray icon that stays quiet that long has to be told apart from one that has stopped
working.

## Decision

- A periodic reminder that falls due while `SuppressionPolicy` would hold it back is
  **postponed**: `HandleTick` replaces the whole interval `TryConsumeDue` has just re-armed with
  `IntervalSchedule.Postpone(now, RetryInterval)`, and the conditions are looked at again when
  that gap has passed, and after every gap, for as long as it takes. The reminder appears on the
  first look after the condition ends, and a whole interval follows it.
- The gap is `ReminderSettings.RetrySeconds`, "Check again every N seconds" in Settings: default
  `DefaultRetrySeconds` (20), from `MinRetrySeconds` (5) to `MaxRetrySeconds` (300), one value
  shared by both pauses and editable while either is ticked.
- An unlock reminder that would be held back is **skipped**: `HandleUnlock` returns without a
  window and without a retry, and the countdown the unlock restarted runs on.
- There is no cap on how long a reminder is held back.
- The reason is kept in `ReminderCoordinator.Suppression`, and the tray tooltip shows it, "paused
  while your mic is on" or "paused for full screen", in place of the countdown (ADR 0025). When
  both pauses apply the reason is the microphone, because "a reason that flickered between the
  two would look like a fault" (the comment in `SuppressionPolicy.Evaluate`).

## Alternatives rejected

- **A one-shot retry for the unlock reminder too.** "The unlock already restarted the schedule,
  so a reminder is coming; a one-shot retry beside the periodic one would be a second deadline
  to keep in step with the first" (`CLAUDE.md`).
- **Asking again on every one-second tick instead of after a gap.** It "would walk the registry
  1800 times per reminder" (README, "Design decisions"); ADR 0019.
- **A cap, after which the reminder appears anyway.** "No cap on how long a reminder is held
  back. That is the requirement" (`CLAUDE.md`). A live microphone is read as "do not interrupt",
  "the intended reading" (README), and the tooltip keeps the reason "one hover away" instead.

## Consequences

- Through an hour-long call, at the default gap, the conditions are looked at every 20 seconds
  and no window appears; the tooltip says why the whole time.
- A postponement does not outlive its schedule: an unlock and `ApplySettings` start a whole new
  interval, a lock suspends the countdown, and all three clear `Suppression`. A gap longer than
  the interval is not mistaken for a clock jump (ADR 0013).
- The gap's bounds have their reasons: below 5 s "the check costs more than the wait", and past
  300 s "a call that ended is a break not taken for longer than the shortest interval the app
  allows" (the `ReminderSettings` comments).
- Guards: `ReminderCoordinatorTests` (a due reminder held back by the microphone and by full
  screen; looked at again after the retry gap; an hour of retries with no window; shown on the
  first retry after the call, with a whole interval after it; the unlock reminder skipped during
  a call, a whole interval still running; an unlock, a lock and `ApplySettings` forgetting the
  held-back reminder), `TrayTooltipTests` (a held-back reminder says why instead of counting
  down) and `SuppressionPolicyTests` (the microphone is the reason when both apply).

Source: CLAUDE.md ("Pausing for what the user is doing"); README.md ("Pausing for what you are
doing", "What it will and will not catch", "Design decisions" row "Held-back reminder");
src/MindfulnessReminder.Core/ReminderCoordinator.cs (the summary, `HandleTick`, `HandleUnlock`,
`HandleLock`, `ApplySettings`); src/MindfulnessReminder.Core/IntervalSchedule.cs (`Postpone`);
src/MindfulnessReminder.Core/ReminderSettings.cs; src/MindfulnessReminder.Core/SuppressionPolicy.cs;
src/MindfulnessReminder.Core/TrayTooltip.cs.
