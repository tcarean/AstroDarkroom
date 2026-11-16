# 0106. No image is taken while a run lasts

Date: 2026-11-16
Status: accepted 2026-11-16

## Context

The top bar's "Open image..." button is disabled while a run lasts; the same button on the
first screen was not, and that panel is visible during a run (no result yet, no error). The busy
overlay stops the mouse and not the keyboard, so Space or Enter on the button that kept the
focus after the file dialog opened the dialog again. `MainViewModel.OpenAsync` had no busy guard:
it replaced the path, and the run it asked for returned at once because one was on. The code
review of 2026-11-13 (CR-009) followed the case: image A's run ended and filled its tabs under
image B's path, with no message, and B was never analysed. ADR 0097 left it open.

## Decision

While a run lasts no image can be opened. Both "Open image..." buttons are bound to `IsIdle`,
and `OpenAsync` called during a run changes nothing and says so in the log, as `ChooseProfile`
and `ClearProfile` already do.

## Alternatives rejected

- **Keep the second image and run it when the first run ends.** The window has no way to say
  that a second analysis is queued, and no way to cancel the first; a wait twice as long behind
  an overlay that names one image is worse than a disabled button.
- **Disable the button only.** `OpenAsync` would still take a path during a run from any other
  caller.

## Consequences

- The state "one image's result under another's path" can no longer be reached, so the test
  that reached it through this defect
  (`R111_AResultIsDroppedForAnotherImageThanItsOwnWhateverTheBarReads`, of 2026-11-14) was
  replaced, at the owner's word of 2026-11-16. `OpenAsync` still compares the chosen path with
  the result's own image, not with the bar's.
- Guards: `R111_ASecondImageChosenDuringARunChangesNothing`,
  `R110_BothOpenButtonsAreDisabledWhileARunLasts`.
