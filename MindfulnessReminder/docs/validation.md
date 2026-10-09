# Validation

In the projects made from the kit this file is the memory of "how well it works, measured on
what": every case lives in `validation.json`, read by `ValidationTests`, and is explained here.
Mindfulness Reminder has no such case (ADR 0005). The application shows a window, keeps a
deadline on the wall clock and reads two registry keys; it has no numeric output and no input
data to measure on. The numbers it carries are constants in the code, which the tests use by name:
`TrayTooltip.MaxLength` (63), `ReminderSettings.MaxMessageLength` (50), the interval bounds 1
to 480 minutes, the retry bounds 5 to 300 seconds, `ReminderCoordinator.DefaultDebounce`
(2 s), `DefaultTickInterval` (1 s), `DefaultActivityRefresh` (10 s),
`WpfReminderPresenter.DefaultShowDelay` (600 ms). A case here would measure a value the code
states outright; a changed value changes its requirement row first, not a figure.

`validation.json` therefore lists no case. `/validate` stops after its first step, `/release`
skips its figures step, and `RepoInvariantTests` checks only that the file parses and that case
IDs are unique, so the shape is guarded when the first case arrives.

## What would become a case

A figure measured on a machine rather than asserted by a test, if one is ever needed to defend
a design choice with a number:

- the time `WindowsActivityMonitor.Read` takes to walk the capture records, which is the reason
  the reading is taken every 10 s and never on the one-second tick (ADR 0019);
- the time between an unlock and the reminder window being on top, which is the reason for the
  600 ms show delay (ADR 0026).

Either would need a `ValidationTests` class with the trait `Category=Validation`, a case in
`validation.json` with a `tolerance`, `kit.json`'s `testFilter` set to `Category!=Validation`,
and a dated line under History. Until then there is nothing to re-measure, and this file says
so rather than pretending otherwise (the kit's rule "Measured numbers").

## History

- 2026-10-09: the file created with no case (ADR 0001, ADR 0005).
