# 0029. Unlock detection is SystemEvents.SessionSwitch behind ISessionMonitor; Locked exists only to suspend the schedule, and a lock never shows a reminder

Date: 2026-10-09
Status: accepted 2026-10-09

## Context

Retroactive: recorded on 2026-10-09 from `README.md` ("Design decisions", row "Unlock
detection"), `CLAUDE.md` ("Structure", the seams and the adapters lines; "The periodic
reminder", the decision "A lock suspends the schedule; an unlock restarts it in full") and the
comments of `ISessionMonitor.cs` and `SystemEventsSessionMonitor.cs`; the decision itself dates
from 61a0121 (the base version, 2026-08-28), which forwarded the unlock alone, and from b6554d5
(the periodic reminder, 1.1.0, 2026-09-08), which added `Locked`. The first reminder this
application had is the one on a workstation unlock. `SystemEvents.SessionSwitch` reports an
unlock among other reasons that are not one, `SessionLogon` and the remote and console connect
reasons of an RDP reconnect, and it "raises its callbacks on a private worker thread, not on
ours". Until b6554d5 the monitor raised `Unlocked` and nothing else. The periodic reminder needed
the other half: "without a lock event its deadline would pass behind the lock screen and the
window would be created where nobody can see it" (`ISessionMonitor`), and "it would then already
be open when the unlock reminder tried to appear, so the coordinator would raise it instead of
showing one" (`CLAUDE.md`).

## Decision

- `ISessionMonitor`, in Core, raises `Unlocked` and `Locked` on the UI thread once `Start` has
  been called. Its one implementation, `SystemEventsSessionMonitor` in the App, subscribes to
  `SystemEvents.SessionSwitch`, forwards `SessionUnlock` as `Unlocked` and `SessionLock` as
  `Locked`, ignores every other reason, marshals both onto the WPF dispatcher with
  `BeginInvoke`, and unsubscribes in `Dispose`.
- `ISessionMonitor` "raises `Locked` for this and nothing else": the periodic schedule's
  suspend. `ReminderCoordinator.HandleLock` suspends the schedule, stops the timer, clears the
  countdown and forgets a held-back reminder; "no reminder is ever shown on lock".
  What an unlock does is ADR 0014's and ADR 0015's.

## Alternatives rejected

- **Forwarding `SessionLogon`.** It "would pop the window during sign-in".
- **Forwarding the remote and console connect reasons.** They "belong to RDP reconnects rather
  than to a real unlock".
- **`WTSRegisterSessionNotification`.** Not taken: `SystemEvents.SessionSwitch` is "A few lines
  behind `ISessionMonitor`", and the interface leaves room for an implementation on the native
  notification "without touching anything else" (README).
- **An unlock event alone, as from 61a0121 to b6554d5.** Enough while the only reminder came on
  unlock; with a schedule, a deadline falls due behind the lock screen.

## Consequences

- The coordinator never sees `SystemEvents`: every rule about locks and unlocks is tested against
  a fake monitor (ADR 0008), and a replacement only has to raise the same two events on the UI
  thread.
- Windows "can raise SessionUnlock twice for a single unlock" (`ReminderCoordinator`); the
  coordinator's debounce collapses the pair (ADR 0015).
- `Dispose` must keep unsubscribing: "Leaving this subscribed keeps the process pinned by
  SystemEvents' own thread."
- Guards: `ReminderCoordinatorTests`, through its `FakeSessionMonitor` (a lock never shows a
  reminder; a lock stops the timer and clears the countdown; an unlock shows the reminder;
  `Start` starts the monitor; `Dispose` unsubscribes and disposes it). `SystemEventsSessionMonitor`
  itself: none by test; launch the application from the tray, then lock and unlock the
  workstation.

Source: README.md ("Design decisions", row "Unlock detection"); CLAUDE.md ("Structure"; "The
periodic reminder"); src/MindfulnessReminder.Core/ISessionMonitor.cs;
src/MindfulnessReminder.App/SystemEventsSessionMonitor.cs;
src/MindfulnessReminder.Core/ReminderCoordinator.cs;
tests/MindfulnessReminder.Tests/ReminderCoordinatorTests.cs.
