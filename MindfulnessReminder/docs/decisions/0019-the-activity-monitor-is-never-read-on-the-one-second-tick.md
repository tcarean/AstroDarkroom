# 0019. The activity monitor is read when a reminder is due, once per retry, and every 10 s when idle; never on the one-second tick

Date: 2026-10-09
Status: accepted 2026-10-09

## Context

Retroactive: recorded on 2026-10-09 from `CLAUDE.md` ("Pausing for what the user is doing", the
rule "The activity monitor is read when a reminder is due, once per retry, and otherwise once
every `DefaultActivityRefresh` (10 s)"), `README.md` ("Pausing for what you are doing", the
paragraph "The tray tooltip says so while it is happening"; the "Design decisions" row "Tooltip
freshness") and the comments on `DefaultActivityRefresh`, `Suppression`, `Evaluate` and
`RefreshSuppression` in `ReminderCoordinator.cs`; the decision itself dates from 89de6c8
(pausing for a call or full screen, 1.2.0, 2026-09-08). A reading walks the capture records of
ADR 0016, "a few hundred registry keys -- about five milliseconds" (the `DefaultActivityRefresh`
comment), and the timer ticks every second (ADR 0012). As first written, the monitor was read
only when a reminder was due, so `Suppression` became non-`None` only at the moment one was held
back: "hovering the tray during a call would count down to a reminder that is not going to
arrive, and the whole feature would be invisible until the interval happened to elapse. That is
exactly how it was first written, and it was wrong" (`CLAUDE.md`). No commit holds that version;
89de6c8 already has the refresh. Two sources disagree with the code. The summary of
`IActivityMonitor` describes the first version: the monitor "is asked ONLY at the moment a
reminder is due, and once per retry while one is being held back", "two registry reads an hour";
the code also reads on the idle refresh. The README's "Tooltip freshness" row calls the refresh
"200× cheaper than every tick"; with the code's 10 s refresh against its 1 s tick, it reads a
tenth as often as every tick would.

## Decision

The activity monitor is read in three places, all in `ReminderCoordinator`, and nowhere else:

- when a periodic reminder is due, and once per retry while one is held back (`HandleTick`, when
  `TryConsumeDue` says yes);
- on an unlock that gets past the unlock switch, the already-showing check and the debounce,
  last because it is the check that reads the registry (`HandleUnlock`; ADR 0015, ADR 0021);
- on a tick where nothing is due, once the last reading is `DefaultActivityRefresh` (10 s) old,
  purely to keep `Suppression`, and with it the tooltip, current (`RefreshSuppression`).

Never on every one-second tick. With both pauses off (`PausesForAnything` false) it is not read
at all. A lock clears the time of the last reading, so the first tick after the unlock takes a
fresh one rather than trust one from before the lock; `ApplySettings` clears it too, so a pause
switched on shows in the tooltip on the next tick rather than up to a refresh later.

## Alternatives rejected

- **Reading on every one-second tick.** It "would be 1800 registry walks per reminder"
  (`CLAUDE.md`), and "a tray application that walks the registry once a second forever has
  stopped being unobtrusive" (the `RefreshSuppression` comment).
- **Reading only when a reminder is due**, as first written. During a call the tooltip counted
  down to a reminder that was not going to arrive; the refresh "is the difference between a
  feature the user can see working and one they have to wait half an hour to catch in the act"
  (the `Suppression` comment).

## Consequences

- Joining or leaving a call shows in the tooltip within ten seconds, "faster than anyone can
  hover the tray icon after joining a call" (the `DefaultActivityRefresh` comment), at a tenth
  of the 1800 readings a 30-minute interval would cost on every tick.
- The "about five milliseconds" of a reading is the comment's estimate, not a figure on record;
  measuring it would be this project's first validation case (ADR 0005).
- Guards: `ReminderCoordinatorTests` (100 idle one-second ticks read the monitor 10 or 11 times;
  joining and leaving a call reach `Suppression` within one refresh, with no reminder due; one
  reading per retry through an hour-long call; with both pauses off the monitor is never read;
  `ApplySettings` takes a fresh reading on the next tick).

Source: CLAUDE.md ("Pausing for what the user is doing"); README.md ("Pausing for what you are
doing", "Design decisions" row "Tooltip freshness");
src/MindfulnessReminder.Core/ReminderCoordinator.cs (`DefaultActivityRefresh`, `Suppression`,
`HandleTick`, `HandleUnlock`, `HandleLock`, `ApplySettings`, `Evaluate`, `RefreshSuppression`);
src/MindfulnessReminder.Core/IActivityMonitor.cs.
