# 0063. File names against the times of the frames: a warning, not another order

Date: 2026-11-16
Status: accepted 2026-11-16

## Context

The frames of a folder are sorted by file name (R-002), and the analysis reads that order as
the order they were taken in: the cadence takes its start from the first frame of the list and
each pause against the frame before (a step back is clamped to 0 s), check 4 takes the last
five frames of the list as the end of the series, and check 2 names "the first" and "the last"
light. The code review of 2026-11-13 (CR-013, `owner`) gave the cases where the two orders
part: darks renamed to `dark (1).NEF` .. `dark (20).NEF`, which sort 1, 10 .. 19, 2, 20, 3 ..
9, so that "the end of the series" is frames 5 to 9 in time, still warming; and a counter that
passes from `DSC_9999` to `DSC_0001` inside a series, on which a warming series reads "the
camera cooled". The warming, the frames to discard and the verdict of check 4 are then read on
a scrambled series, and nothing says so. LightsCheck had the same defect and its owner, the
same, chose a warning on 2026-11-14 (LightsCheck's ADR 0054). The owner chose the same here on
2026-11-16.

## Decision

The order stays the names'. When a frame with a time was taken before the frame with a time
listed before it, the session lines carry a `WARNING` row under the group's `interval` row, in
the console and in the window's session card, naming the first such pair and saying what
follows the names and may be wrong: for the darks the pauses, the warming of check 4 and the
frames to discard; for the lights the pauses and the first and last light of check 2 (R-126).
The darks are the ones analysed, the partner's when they stand in for the session's (R-054).
No figure, no frame number and no exit code changes.

## Alternatives rejected

- **The frames taken in the order of their times when every frame has one.** It gives the
  right figures on such a folder. It changes what frame N is in every table and in the files
  written, and needs a rule for the night the clock is set back, when the times, followed,
  would spoil a series whose names are right. LightsCheck left it as a feature of its own; the
  two tools stay alike.
- **Check 4 unmeasured on such a series.** The reader loses the figures of a series whose
  names may be only slightly out of order (two frames swapped); the line lets them judge.
- **Nothing.** The verdict of check 4 on such a series is wrong with no sign of it.

## Consequences

On a folder whose names are not in the order of the times check 4 is still read on the names'
order, and the session lines say so; the remedy is names that sort in the order of the night.
The night the clock is set back gets the line too, though its names are right: the pause
across the repeated hour is wrong there today, and the line is true of it. Equal times do not
count, since a raw's time is in whole seconds, so biases shot several a second would not
trip it; the biases get no line in any case, nothing of theirs is read from one frame to the
next in time. The line does not make the run exit 1, which stays the sign of a frame or a
group that could not be used. `Cadence.Backwards` holds the pair and the log's cadence line
has it. Guarded by `R126_AFrameTakenBeforeTheOneListedBeforeItIsFound`,
`R126_TheSessionLinesSayWhenTheNamesAreNotInTheOrderTheFramesWereTaken`,
`R126_TheConsoleWarnsWhenTheNamesAreNotInTheOrderTheFramesWereTaken_Exit0`,
`R126_ThePartnersDarksOfALightsOnlySessionGetTheLineToo` and, in the window,
`R126_TheSessionCardWarnsWhenTheNamesAreNotInTheOrderTheFramesWereTaken`.
