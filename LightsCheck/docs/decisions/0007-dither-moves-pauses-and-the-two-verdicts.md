# 0007. A dither move is a registered step of 5 px or more; a pause is 5 s; two verdicts, hot pixels and walking noise

Date: 2026-10-09
Status: accepted 2026-10-09

## Context

The owner's question on the Veil session was "how often did I dither, how much, and where
was it too rare or too small". The hand analysis counted the pauses between exposures (the
intervalometer gap tells where the mount was touched) and separately the steps the
registration measured, and found 47 pauses but only 38 visible moves: nine presses did
nothing (the DEC backlash). The owner asked for a table of "every 1, 2, 3 frames", a size
table, and a verdict, with the pixel values quoted. The thresholds for "enough" are rules of
thumb from the DSLR community (a few pixels break hot pixels; 10 px or more, random, in two
dimensions, break walking noise), not measurements.

## Decision

A move is a step between consecutive registered frames of at least `Th.DitherStep` (5 px:
the first run with 3 px counted 15 of the Veil session's back-to-back drifts of 3 to 7 px as
moves, and the hand analysis had called the presses under 5 px "without effect"); a pause is
a gap before a frame above `Th.DitherPause` (5 s); both are counted, and so are the pauses
without a move and the moves without a pause, so a press that did nothing is visible. "How
often" is the table of frames between consecutive moves plus the longest stretch without
one; "how much" the median and the classes [5, 10), [10, 20), 20+ px
with the axes and the signs; "coverage" the nearest-other-frame distances, the counts within
3, 5 and 10 px, the isolated frames, the distinct spots by greedy clustering at 5 and 10 px,
and the runs of 3 or more consecutive frames within 5 and 10 px. Two verdicts, each with its
numbers: hot pixels fine while no more than 3 consecutive frames share a spot within 3 px;
walking noise fine when at least half the frames are distinct at 10 px, at least 30 % are
isolated at 10 px, and each axis carries at least a quarter of the moves. The frame classes
use the owner's thresholds (1.35 marginal, 1.6 bad) and every flag carries its value and its
threshold.

## Alternatives rejected

- **Defining a move by the pause alone.** It would count the nine presses that moved
  nothing as dithers; the step is what the sensor saw.
- **A single "dithering score".** The owner asked for the tables; a score would hide which of
  the three things (frequency, size, direction) was lacking.
- **Thresholds from the data.** There is one session; the community's rules of thumb are
  stated as such in the report, and `Th` is the place to tune them after more sessions.

## Consequences

`DitheringTests` pins the tables on hand-made patterns; V-008 pins the Veil figures. The
verdict text names the thresholds it applied, so a change in `Th` shows in the goldens.
