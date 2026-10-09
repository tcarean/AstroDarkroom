# The periodic reminder: one timer, one deadline on the wall clock

> Moved unchanged on 2026-10-09 (ADR 0001) from `CLAUDE.md`, "The periodic reminder". Where a
> paragraph became a decision record, its number is given after it. **Covers:**
> `src/MindfulnessReminder.Core/ReminderCoordinator.cs`,
> `src/MindfulnessReminder.Core/IntervalSchedule.cs`,
> `src/MindfulnessReminder.Core/IPeriodicTimer.cs`, `src/MindfulnessReminder.Core/IClock.cs`,
> `src/MindfulnessReminder.Core/ISessionMonitor.cs`,
> `src/MindfulnessReminder.App/DispatcherPeriodicTimer.cs`. A change to the code this describes
> means this note is updated in the same task, or deleted with the decision it explains.

## The periodic reminder

Added in 1.1.0. The shape of it is more considered than the feature looks, and the four
decisions below are load-bearing.

**One timer, ticking every second, driving a deadline.** `IPeriodicTimer` is *not* armed for
the reminder interval. It ticks at `ReminderCoordinator.DefaultTickInterval` (1 s) and
`IntervalSchedule` holds a deadline that each tick is compared against. Two things need that
tick — deciding whether the interval has elapsed, and redrawing the tooltip countdown — and
a one-shot timer armed for the interval would have needed a second timer beside it. The
requirement was explicitly for one timer.

See ADR 0012.

**The deadline is on the wall clock, never a count of elapsed ticks.** A `DispatcherTimer`
does not tick while the machine sleeps, so accumulating ticks would silently stretch a
30-minute interval by however long the lid was shut. Comparing against `IClock.UtcNow`
instead means a reminder that came due during sleep fires on the first tick after waking,
which is the wanted behaviour. Do not "fix" this into a stopwatch.

See ADR 0013.

**A lock suspends the schedule; an unlock restarts it in full.** `ISessionMonitor` raises
`Locked` for this and nothing else — no reminder is ever shown on lock. Without the suspend,
the deadline would pass behind the lock screen, the window would be created where nobody can
see it, and it would then already be open when the unlock reminder tried to appear, so the
coordinator would raise it instead of showing one. `Resume` grants the whole interval afresh
regardless of how long the lock lasted, which is the requirement, and is why no time is
carried across a lock.

See ADR 0014; `ISessionMonitor` and its `Locked` event are ADR 0029.

**The clock can move backwards.** An NTP correction or a hand-set clock would otherwise
postpone the reminder by however far it jumped. `TryConsumeDue` notices a deadline further
out than a whole interval could put it (the span it was armed with, since 89de6c8: ADR 0013)
and starts over. The other direction needs no guard
and must not get one — see the sleep case above.

See ADR 0013.

Two smaller rules that are easy to break:

- `HandleUnlock` restarts the schedule **before** the debounce and the already-showing
  check. Those two decide whether a *window* appears; the schedule is not theirs to skip.
- `TryConsumeDue` re-arms as it returns true, so a due reminder that cannot be shown (one is
  already on screen) still leaves a sane schedule instead of a deadline stuck in the past
  that fires on every subsequent tick.

See ADR 0014.

Settings are applied by `ApplySettings`, which **restarts the countdown from zero**. Keeping
the elapsed time would mean changing 30 minutes to 10 with 25 gone either fires immediately
or leaves the tooltip showing a number the new setting cannot explain.

See ADR 0014.
