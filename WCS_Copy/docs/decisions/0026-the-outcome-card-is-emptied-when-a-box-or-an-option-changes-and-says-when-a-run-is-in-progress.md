# 0026. The outcome card is emptied when a box or an option changes, and says when a run is in progress

Date: 2026-11-18
Status: accepted 2026-11-18

## Context

The outcome card kept the last outcome until the next run ended (CR-009 of the review of
2026-11-16): a Preview of A into B still listed A's cards under boxes that named C, or after the
optical keys were turned off; and a refused copy still showed `Refused` while a forced copy of a
large stack was being written, with nothing on screen but two greyed buttons. A copy of the 290 MB
Veil stack takes 0.3 s warm and 1.3 s cold (measured on 2026-11-18). The entry offered two ways,
to empty the card or to dim it; the owner chose to empty it on 2026-11-18, with a line of its own
while a run is in flight.

## Decision

The card is emptied when the value of a box's text, of the in-place or new-file choice, or of
either option changes, and when a Preview or a Copy starts; while the run is in flight it shows
only `Preview in progress...` or `Copy in progress...` (`Previzualizare in curs...`,
`Copiere in curs...`), the window's own pairs, and then the run's outcome (R-053). A box changed
during a run does not hide that run's outcome: a copy's lines say a file was written. Help opens
the window's section while the card holds no outcome (R-039).

## Alternatives rejected

- **Dim the old outcome until the next one comes.** The owner chose to empty it: a dimmed list
  of cards still reads as an answer about the files on screen.
- **Hide the outcome of a run whose boxes changed while it ran.** A copy that ran wrote a file;
  the card is the only place that says so and where.
- **Say nothing during a run, as before.** On a large stack and a cold disk the window then
  showed the last refusal for the seconds the copy took.

## Consequences

The view-model's `HasCard` (an outcome, or a run in flight) drives the card's visibility and
`RunningLine` its first row; `HasOutcome` stays the outcome's own flag, which Help reads. A held
close (R-050) shows the in-progress line in the greyed window. Guarded by
`MainViewModelTests.R053_AChangeOfABoxOrAnOptionEmptiesTheCard` (the six changes),
`R053_AValueSetToWhatItIsKeepsTheCard`, `R053_DuringARunTheCardSaysTheRunIsInProgress`,
`HelpTests.R039_DuringARunHelpOpensTheWindowsSection` and the held-run renders of
`WindowSmokeTests`.
