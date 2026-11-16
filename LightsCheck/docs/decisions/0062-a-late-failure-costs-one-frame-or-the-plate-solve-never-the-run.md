# 0062. A late failure costs one frame or the plate solve, never the run

Date: 2026-11-16
Status: accepted 2026-11-16

## Context

A series run measures every light, which takes minutes, then aligns the frames, reads the
biases and looks for a plate-solved FITS at the session root. The loop that measures catches
whatever one frame throws, keeps the frame with its error and goes on (R-026), and the read
noise catches everything too and is then missing from the report. The two steps between and
after them did not. The alignment loop had no catch: the least-squares fit dereferences the
null `Stats.Solve` gives for a system it cannot solve, and one such frame ended the run. The
search for a plate solve caught `IOException` and `InvalidDataException` only, so a FITS at the
session root that Windows refuses to open (`UnauthorizedAccessException`) ended the run as
well, after every frame had been measured. The review of 2026-11-13 (CR-015, `confirmed` for
the plate solve; no input was found that makes the fit throw).

## Decision

What fails after the frames are measured costs only what it was reading:

- A fit without a solution is a failed registration (`Registration.Align`), and the alignment
  loop catches whatever a frame's registration throws (`SeriesRun.RegisterGuarded`): the frame
  is `could not be aligned`, with a Warn line and the exception in the log.
- A FITS at the session root that cannot be read, for any reason, is skipped as a plate-solve
  source, with its line in the log, and the next one is tried (`SeriesRun.ReadWcs`). A session
  folder that cannot be listed then (the drive unplugged or the folder renamed during the
  measuring; or a folder that refuses to be listed, which a session with a `lights\` subfolder
  meets here for the first time) gives no plate solve, with a Warn line (added after the
  review of 2026-11-16).

The report is written from what was measured. A cancel is never caught.

## Alternatives rejected

- **Catching the two exception types that were seen.** The next type nobody thought of ends
  the run again after the minutes of measuring; the measuring loop and the read noise already
  catch everything for that reason.
- **Catching every exception around the whole run in the console and the window.** A failure
  of the analysis itself is a defect to see, with its stack, not a line to print; the five
  types both applications catch stay as they are.
- **Changing `Stats.Solve` to throw.** `Stats.cs` is a copy of FlatCheck's and stays one
  (ADR 0003).

## Consequences

A catch that takes everything can hide a defect in the alignment behind `could not be
aligned`; the Warn line with the exception's stack is what tells the two apart, and a session
where every frame fails that way would show it at once. Such a frame's reason still reads `too
few stars in common with the reference frame`, the one reason a frame without a position has;
the log has the true one. `FitSimilarity` returns null where it used to throw, so its callers
check. The test of the plate solve sets a rule on a file that denies reading it, which only
Windows has; elsewhere it passes without checking anything. No test checks that the alignment
loop goes through `RegisterGuarded`: the guard is tested alone, and the loop's one call is read.
Guarded by
`R018_AFitWithoutASolutionIsAFailedRegistration`,
`R018_TheSimilarityFitHasNoSolutionWhenEveryPairIsOnePoint`,
`R026_ARegistrationThatThrowsIsAFrameThatCouldNotBeAligned`,
`R025_AFitsAtTheRootThatCannotBeOpenedIsSkippedAsThePlateSolve` and
`R025_ASessionFolderThatCannotBeListedGivesNoPlateSolve`.
