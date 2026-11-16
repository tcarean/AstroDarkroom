# 0063. A block of lit frames is found by a second fit, taken only when it finds one

Date: 2026-11-16
Status: accepted 2026-11-16
Supersedes: 0012 in part (the per-frame fit by Theil-Sen alone)

## Context

Check 3 names the frames that carry light by fitting each frame's mean on the warming of the
series and clipping what lies over the line (R-028, ADR 0012). The line is Theil-Sen's, the
median of the pairwise slopes, chosen because the five lit frames of the leak set (5 of 30) do
not tilt it. The code review of 2026-11-13 (CR-014, `to measure`) read where it gives way:
near 29 % of bad points the pairs that join a clean frame to a lit one outnumber the others,
the line tilts through the lit frames, the scale grows and the first pass finds nothing over
the limit. A lamp lights the frame evenly, so the map's edges do not depart either, and the
check reads `OK` on a series a third of which carries light.

Measured on 2026-11-16 with a copy of the fit in Python, kept outside the repository, on
generated series (the ratio rising evenly from 0.8 to 1.2, the lit frames at the warm end, a
light of 30 times and of 12 times the frames' scatter; 300 series each, the share in which
exactly the lit frames are named):

| Frames, lit | Theil-Sen alone | With the second fit |
|---|---:|---:|
| 6, 2 | 0 % / 0 % | 100 % / 88 % |
| 8, 3 | 0 % / 0 % | 100 % / 70 % |
| 12, 4 | 2 % / 0 % | 100 % / 91 % |
| 20, 7 | 0 % / 0 % | 100 % / 89 % |
| 30, 11 | 0 % / 0 % | 100 % / 86 % |
| 30, 14 | 0 % / 0 % | 100 % / 7 % |
| 30, 5 | 100 % / 95 % | the same: the first fit stands |

And the price, a clean series with a frame named by mistake (4000 series each, the scatter
above the 0.1 ADU-eq floor; with the libraries' scatter of 0.02 none either way):

| Frames | Theil-Sen alone | Second fit always taken | Second fit taken only for a block |
|---|---:|---:|---:|
| 6 | 8.8 % | 23.6 % | 13.9 % |
| 8 | 5.8 % | 14.9 % | 6.5 % |
| 12 | 3.0 % | 6.6 % | 3.2 % |
| 20 | 1.3 % | 2.4 % | 1.3 % |
| 30 | 0.4 % | 0.8 % | 0.4 % |

On the per-frame figures of three real sets (the leak set of 2026-10-20, the library of
2026-10-10, the Smida darks) both name the same frames, 26 to 30 on the leak set and none on
the other two, with the same scale. The owner chose on 2026-11-16.

## Decision

The passes of R-028 are run twice. The first run is the one of ADR 0012. The second starts
from a line that holds while fewer than half of the frames are lit: among the lines through
two frames, each with the median of the residuals as its intercept, the one whose median
absolute residual is smallest, read again by Theil-Sen over the frames within
`Th.LeakBlockInlierSigmas` (2.5) scales of it. The figures of either run are Theil-Sen's over
the frames it kept. The second run's result replaces the first's only when it names more lit
frames than the first, more than `Th.LeakBlockMinFraction` (a quarter) and fewer than
`Th.LeakBlockMaxFraction` (half) of the fitted frames, and leaves a smaller scale. On every
other series the figures are the first run's, as before.

## Alternatives rejected

- **The second line always.** It names the block as well, and doubles the clean series with a
  frame named by mistake (the table above): a line read from half of a short series misreads
  the other half.
- **The median of the medians of each frame's slopes (repeated medians).** Its false alarms
  are near Theil-Sen's, but it holds only to about 35 % and only for a strong light: 30 frames
  with 11 lit were named in 36 % of series at 30 times the scatter and in none at 12 times.
- **Say in the row that over a quarter of lit frames would not be seen.** Honest and free, and
  it finds nothing.
- **Leave it.** The check then reads `OK` on the series the entry describes.

## Consequences

A series with a block of lit frames between a quarter and half of its length reads those
frames and `WARNING`; with half or more lit, which half carries the light cannot be told, and
the check still names none. A light of less than about ten times the frames' scatter in such
a block is found only sometimes (the 12-times column); a strong one always. At six frames, the
least the fit reads, the block rule costs false alarms (13.9 % against 8.8 %): two frames are a
third of six. That Theil-Sen alone names a clean frame in 3 to 9 % of short series whose
frames scatter more than the floor is older than this change and is CR-078 of the review list,
open. The three constants are in `Thresholds.cs`. `/validate` reads the three real series as
before (the plan `docs/plans/2026-11-16-fix-cr-011-to-cr-015.md` has the table). Guarded by
`R028_ABlockOfLitFramesOverAQuarterOfTheSeriesIsNamed` and
`R028_TheBlockStartHoldsWhereTheMedianOfThePairwiseSlopesTilts`; the earlier R028 tests keep
their figures.
