# 0013. The deadline is on the wall clock, never a count of ticks; only a backwards clock jump is guarded, against the span the deadline was armed with

Date: 2026-10-09
Status: accepted 2026-10-09

## Context

Retroactive: recorded on 2026-10-09 from `CLAUDE.md` ("The periodic reminder", the second and
fourth decisions; "Pausing for what the user is doing", the rule "`IntervalSchedule` compares the
backwards-clock guard against `_armedSpan`"), `README.md` ("Periodic reminders", the second
bullet) and the comments of `src/MindfulnessReminder.Core/IntervalSchedule.cs`; the decision
itself dates from b6554d5 (the periodic reminder, 1.1.0, 2026-09-08) for the wall clock and the
backwards-clock guard, and from 89de6c8 (pausing for a call or full screen, 1.2.0, 2026-09-08)
for the armed span. "A `DispatcherTimer` does not tick while the machine sleeps, so accumulating
ticks would silently stretch a 30-minute interval by however long the lid was shut"
(`CLAUDE.md`). The clock itself can move: "An NTP correction or a hand-set clock would otherwise
postpone the reminder by however far it jumped" (the same section). In b6554d5 `TryConsumeDue`
restarted the countdown on a deadline further away than a whole interval. 89de6c8 added
`Postpone`, which moves the deadline of a held-back reminder out by the retry gap, up to 300 s,
and against the interval that guard "made every retry gap longer than the interval look like a
clock jump, which reset the schedule and threw the postponement away" (the `IntervalScheduleTests`
summary of the regression test), "so the reminder appeared in the middle of the call after all"
(the same test in `ReminderCoordinatorTests`). No commit holds `Postpone` with the old
comparison: the defect lived only in the work that became 89de6c8. "The periodic reminder" still
says "a whole interval", as does the summary of the backwards-jump test in
`IntervalScheduleTests`; the code has compared against the armed span since 89de6c8.

## Decision

- `IntervalSchedule` keeps a deadline, a point in time, and compares it with the `now` it is
  given; the coordinator gives it `IClock.UtcNow`, which in production is `SystemClock`,
  `DateTimeOffset.UtcNow`. Nothing counts ticks.
- A deadline that passed while the machine slept fires on the first tick after waking.
  `Remaining` reads zero for a deadline in the past, never a negative time.
- A backwards jump of the clock is guarded: `TryConsumeDue` treats a deadline further away than
  `_armedSpan`, the span it was set with (the interval, by `Restart`; the retry gap, by
  `Postpone`), as a jump, and restarts the countdown instead of waiting it out.
- A forward jump has no guard and must not get one: waking from sleep looks the same to the
  schedule, and a reminder that came due during sleep "is genuinely owed" (the
  `IntervalScheduleTests` summary).

## Alternatives rejected

- **A count of elapsed ticks.** It would stretch the interval by every sleep; `CLAUDE.md` says not
  to "fix" the deadline "into a stopwatch".
- **No guard against a backwards jump.** The reminder would be postponed "by however far it
  jumped" (`CLAUDE.md`).
- **The guard compared against the interval**, as b6554d5 wrote it. With `Postpone`, a retry gap
  longer than the interval, "300 s against a 1-minute interval", would "look like a clock jump,
  reset the schedule and throw the postponement away" (`CLAUDE.md`, "Pausing for what the user is
  doing").
- **A guard against forward jumps too.** It would catch sleep and hold back a reminder that is
  owed: "The other direction needs no guard and must not get one" (`CLAUDE.md`).

## Consequences

- Whatever sets a deadline sets `_armedSpan` with it: `Restart` and `Postpone` today. A third way
  of setting one that left the span alone would bring the regression back.
- `IntervalSchedule` reads no clock of its own: its tests pass times in, and the coordinator's
  tests move a `MutableClock`.
- Guards: `IntervalScheduleTests` (a deadline passed during sleep fires on the first tick after;
  a backwards jump restarts the countdown, also during a postponement;
  `A_postponement_longer_than_the_interval_is_not_read_as_a_clock_jump`; `Remaining` never
  negative) and `ReminderCoordinatorTests` (the same regression through the coordinator, with a
  1-minute interval and a 300 s retry gap).

Source: CLAUDE.md ("The periodic reminder"; "Pausing for what the user is doing"); README.md
("Periodic reminders"); src/MindfulnessReminder.Core/IntervalSchedule.cs;
src/MindfulnessReminder.Core/IClock.cs; src/MindfulnessReminder.Core/ReminderCoordinator.cs;
tests/MindfulnessReminder.Tests/IntervalScheduleTests.cs;
tests/MindfulnessReminder.Tests/ReminderCoordinatorTests.cs; the diffs of b6554d5 and 89de6c8.
