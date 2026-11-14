# 0054. File names against the times of the frames: a warning, not another order

Date: 2026-11-14
Status: accepted 2026-11-14
Supersedes: 0035 in part (the order the Quality tab's line above its charts names)

## Context

The frames of a folder are sorted by file name (R-002), and the series reads that order as the
order of the night: the cadence takes its start from the first frame of the list and each pause
against the frame before, and the trends, the rotation, the drift and the dithering read one
frame against the next. The review of 2026-11-13 (CR-010, `confirmed`) gave the case where the
two orders part: a camera whose counter passes from `DSC_9999` to `DSC_0001` during the night,
or names without leading zeros (`Light_999`, `Light_1000`). Three 120 s frames taken 150 s
apart and named `DSC_9999`, `DSC_0001`, `DSC_0002` give a span of 270 s for 360 s of exposure,
`the camera was exposing 133 % of the time`, and a pause of -420 s; on a whole session the first
ten good frames are the last of the night, and the step across the wrap is a large move without
a pause. Nothing said so. Reading the times of every light before a run costs 0.1 to 0.6 s
(measured on 2026-11-14 on the recorded sessions, 32, 87 and 82 raws, cold and warm disk). The
owner chose on 2026-11-14.

## Decision

The order stays the names'. When a light with a time of capture was taken before the light with
a time listed before it, the session lines carry a `WARNING` after the interval, in the console
and in the window's session card, naming the first such pair and saying that the pauses and
everything read from one frame to the next follow the names and may be wrong (R-089). No
figure, no frame number and no exit code changes. The line above the charts of the window's
Quality tab, which said that each point is a frame `in the order they were shot` (ADR 0035),
says `in the order of the file names` (`in ordinea numelor de fisier`), which is what the
charts show: the owner's word of 2026-11-14, after the review of the branch found the sentence
false on a folder with the warning (R-080).

## Alternatives rejected

- **The frames taken in the order of their times when the names disagree**, with a line saying
  so. It gives the right figures on a night the counter wrapped, and it was the recommended
  one. It changes what frame N is in every table, in `--frame N` and in the files, needs the
  times of every light before any of the three runs starts, and needs a rule for the night the
  clock is set back (the hour that repeats when summer time ends puts a raw's local times in
  another order than the night's, while the names are right): there the times, followed, would
  spoil figures that are right today. Left as a feature of its own.
- **Nothing.** The figures of such a night are wrong with no sign of it.

## Consequences

On a folder whose names are not in the order of the times the report is still wrong where it
reads one frame against the next, and says so on its first lines; the user's remedy is names
that sort in the order of the night. The night the clock is set back gets the warning too,
though its names are right: the pause across the repeated hour is wrong there today, and the
line is true of it. Equal times do not count, since a raw's time is in whole seconds. The
warning does not make the run exit 1, which stays the sign of a frame that could not be read.
`Cadence.Backwards` holds the pair and the log's cadence line has it. Guarded by
`R089_AFrameTakenBeforeTheOneListedBeforeItIsFound`,
`R089_TheTimesAreComparedBetweenNeighboursWithATime`,
`R089_TheSessionLinesSayWhenTheNamesAreNotInTheOrderTheFramesWereTaken`,
`R089_TheConsoleWarnsWhenTheNamesAreNotInTheOrderTheFramesWereTaken`,
`R089_ASeriesWhoseNamesAreNotInTheOrderOfItsTimesSaysSoInTheSessionLines` and, in the window,
`R089_TheSessionCardWarnsWhenTheNamesAreNotInTheOrderTheFramesWereTaken`.
