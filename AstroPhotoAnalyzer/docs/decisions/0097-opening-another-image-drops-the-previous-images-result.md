# 0097. Opening another image drops the previous image's result at once

Date: 2026-11-14
Status: accepted 2026-11-14

## Context

`MainViewModel.OpenAsync` set the new path and then asked for a run; the run checks the fields
first and returns on a bad one before it clears anything. The code review of 2026-11-13 (CR-005)
followed the case: image A is open with its result, a field is wrong (a distance typed `1,5`),
and the user opens image B. The top bar showed B's path while every tab still showed A's
figures and verdict, and the report's default name was beside B, so A's report would have been
saved in B's folder. The owner was asked on 2026-11-14 which of the two ways out the window
takes, and chose the first.

## Decision

Opening a path other than the one the result on screen was measured on, while no run lasts, drops the previous image's
result before the fields are checked: the result is forgotten, the tabs are emptied, the soft
proof's comparison ends as it does after another image's failed run (R-166), and nothing can be
saved. The new path stays chosen; a bad field then shows its message on the error card, and
"Analyse" runs the new image once the field is mended. Opening the same path again keeps the
result until the run replaces it, as before.

## Alternatives rejected

- **Do not take the new image until the fields are valid.** The bar would stay on A with A's
  result, but the choice made in the dialog would be lost and made again after the field is
  mended, and the field's message would sit behind A's tabs (CR-040, open).
- **Clear the result in the run, before the field check, for every run.** "Analyse" pressed on
  the same image with a mistyped field would then throw away a result that is still that
  image's.

## Consequences

- The tabs never show one image's figures under another's path, and the save never names
  another image's folder.
- A second image chosen while a run lasts is CR-009, its own entry, still open: `OpenAsync`
  drops nothing during a run. (Restated 2026-11-16: CR-009 was closed by ADR 0105, no image is
  taken while a run lasts; the second guard below reached its state through that defect and was
  replaced by `R111_ASecondImageChosenDuringARunChangesNothing`.)
- `SoftProofTabViewModel.EndComparison` is called for an image that was only chosen too, and
  its log line says which case it was.
- Guards: `R111_OpeningAnotherImageWhileAFieldIsWrongDropsThePreviousResult`,
  `R111_AResultIsDroppedForAnotherImageThanItsOwnWhateverTheBarReads`,
  `R111_OpeningTheSameImageAgainWhileAFieldIsWrongKeepsItsResult`.
