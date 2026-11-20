# 0116. That the report can be saved is announced when the run ends

Date: 2026-11-20
Status: accepted 2026-11-20

## Context

"Save the report..." is bound to `MainViewModel.CanSave`, which is `HasResult && !IsBusy`. Only
`HasResult` announced a change of `CanSave`, and a run sets `HasResult` while it still counts as
busy; the end of the run, `IsBusy = false`, announced `IsIdle` and `CanRun` alone. So the button
read `CanSave` once, as false, and was never told it had become true: after every analysis
"Open image..." and "Analyse" were enabled again and "Save the report..." stayed grey. The code
review of 2026-11-13 (CR-014) found it by reading and on a replica of the three properties; the
tests asserted the getter and called `SaveReport` directly, and none read the button.

## Decision

The `IsBusy` setter announces `CanSave` with `IsIdle` and `CanRun`: every property a value is
computed from announces it. The button has a name, `SaveButton`, and a test reads it in the real
window before a run, during it and after.

## Alternatives rejected

- **Set `HasResult` after `IsBusy` is cleared.** It mends the order and leaves the rule broken:
  the next property computed from `IsBusy` would meet the same fault.
- **A command with `CanExecute`.** WPF asks it again on its own schedule; the window's other
  buttons are bound to properties, and one way is enough.

## Consequences

- The report can be saved from the window after a run, as R-110 and R-111 say.
- Guards: `R111_ThatTheReportCanBeSavedIsAnnouncedWhenTheRunEnds`,
  `R110_TheSaveButtonIsEnabledOnceARunHasEndedWithAResult`.
