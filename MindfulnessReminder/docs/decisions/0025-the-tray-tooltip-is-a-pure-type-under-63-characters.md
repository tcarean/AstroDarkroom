# 0025. TrayTooltip is a pure type because the tray throws above 63 characters; minutes round up, a pause shows its reason, and only a change is written

Date: 2026-10-09
Status: accepted 2026-10-09

## Context

Retroactive: recorded on 2026-10-09 from `CLAUDE.md` ("The tray tooltip"; "Structure", the
`TrayTooltip.cs` line), `README.md` ("Behaviour", the tooltip bullet; "Pausing for what you are
doing", the paragraph "The tray tooltip says so while it is happening") and the comments of
`TrayTooltip.cs`, `App.UpdateTooltip` and `TrayIconHost.SetTooltip`; the decision itself dates
from b6554d5 (the periodic reminder, 1.1.0, 2026-09-08), which made the tooltip a countdown, and
from 89de6c8 (pausing for a call or full screen, 1.2.0, 2026-09-08), which made the reason for a
pause replace it. `NotifyIcon.Text` "is capped at 63 characters and **throws** above it", and "a
tray icon lost to an exception is a poor trade for a countdown" (`TrayIconHost.SetTooltip`). The
countdown is redrawn off the one-second tick (ADR 0012) but changes once a minute, and "every
write is a `Shell_NotifyIcon` call". Since 89de6c8 a reminder can be held back for as long as a
call lasts (ADR 0018), and what is counted meanwhile is the retry gap, "seconds rather than
minutes".

## Decision

- `TrayTooltip.Format(banner, remaining, suppression)`, in Core, builds the whole hover text as
  a pure function, and `TrayTooltip.MaxLength` (63) is the tray's limit. The limit is the only
  reason it is a type of its own: "the one thing worth testing is that the longest string the
  app can produce still fits".
- With a countdown the text is `<banner> - next in N min`, N rounded **up** and never below 1, so
  a 30-minute countdown reads `30 min` the instant it restarts and never `0 min` through its last
  minute. With no countdown (the periodic reminder off, or the workstation locked) and nothing
  held back, it is the banner alone.
- While `ReminderCoordinator.Suppression` is not `None`, the reason replaces the countdown:
  `<banner> - paused while your mic is on` or `<banner> - paused for full screen`. The README's
  examples are `Mindfulness Reminder 1.4.0 - next in 12 min` and
  `Mindfulness Reminder 1.4.0 - paused while your mic is on`.
- `App.UpdateTooltip` renders the text on every `CountdownChanged` and writes it to the tray only
  when it differs from the string written last: with a per-minute countdown off a per-second
  tick, "1 write in 60".
- A string over 63 characters is cut, never handed to `NotifyIcon.Text`: by `TrayTooltip.Format`,
  and again by `TrayIconHost.SetTooltip`, "the belt to its braces".

## Alternatives rejected

- **The text composed inside the tray host.** Nothing could then show that the longest string
  fits, and "a reason added one day that is a few characters too long would take the tray icon
  down with it" (`TrayTooltipTests`).
- **Minutes rounded down.** "Rounding down would show 29 for the first 59 seconds of every
  interval" (`TrayTooltipTests`), and the last minute would read `0 min`, which "looks broken"
  (`TrayTooltip`).
- **The countdown beside the reason.** What is counted then is the retry gap, and rounding it up
  "would leave the tooltip reading '1 min' for the whole of an hour-long call"; between
  reminders the countdown "would be a promise the app cannot keep -- the number would run down
  and nothing would appear" (`CLAUDE.md`).
- **A write on every tick.** Sixty `Shell_NotifyIcon` calls for each change the user can see.
- **Letting `NotifyIcon.Text` throw.** Cutting the string means "a longer banner one day degrades
  the tooltip instead of taking the tray icon down with it" (`TrayTooltip`).

## Consequences

- Any new wording of the tooltip goes through `TrayTooltip.Format`. The length test builds every
  shape it can take, with the real banner, the longest interval
  (`ReminderSettings.MaxIntervalMinutes`, 480) and every `ReminderSuppression` value, so a new
  reason or a longer version number is measured against the limit without a new test.
- The reason is only as fresh as `Suppression`, which the coordinator re-reads every
  `DefaultActivityRefresh` (10 s) between reminders (ADR 0019); that refresh is what makes the
  README's "within ten seconds" true.
- The banner carries the version (ADR 0011), so hovering the icon says which build is running.
- Guards: `TrayTooltipTests`, with
  `The_longest_tooltip_the_app_can_produce_fits_the_tray_limit`, the test `CLAUDE.md` names; the
  class also pins the rounding, the reason in place of the countdown and the cut of an over-long
  banner. The comparison in `App.UpdateTooltip` and the cut in `TrayIconHost.SetTooltip`: none
  by test; launch the application from the tray and hover the icon (`--preview` has no tray
  icon).

Source: CLAUDE.md ("The tray tooltip"; "Structure"); README.md ("Behaviour"; "Pausing for what
you are doing"); src/MindfulnessReminder.Core/TrayTooltip.cs;
src/MindfulnessReminder.App/App.xaml.cs; src/MindfulnessReminder.App/TrayIconHost.cs;
tests/MindfulnessReminder.Tests/TrayTooltipTests.cs.
