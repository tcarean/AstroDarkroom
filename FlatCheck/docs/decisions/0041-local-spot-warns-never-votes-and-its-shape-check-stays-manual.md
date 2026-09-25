# 0041. The local spot warns above 2 % and never votes, and its round-or-elongated check stays manual

Date: 2026-09-25
Status: accepted 2026-09-25

## Context

Retroactive: recorded on 2026-09-25 from docs/design/modes-structure.md (`LocalSpots`) and
docs/design/pipeline.md (`LocalSpots`); the decision itself dates from when the metric was
added, after the fifth test. `LocalSpots` finds the deepest compact departure after the
large-scale structure is subtracted: fine tiles (80 across, ~18 px at analysis resolution) minus
the median of their 7x7 neighbourhood, largest |difference| wins, with sign and position. A
gradient is followed by the neighbourhood median (0.46 % at a 12 % ramp, 0.68 % at 25 %); a dust
shadow stays whole (4.99 % on a 5 % shadow, 4.71 % with a 12 % ramp and 3 % noise). A dust shadow
is correct for the session whose lights carry the same shadow and wrong for a library flat, and
the tool cannot know which use is intended. On the real Nikkor 35 f/2.8 master the dust shadow
visible in the map is −0.31 %.

## Decision

The local spot is informative, with a warning above `Th.LocalSpotWarn` (2 %), never a verdict.
The sign is kept and named in words: `StructureReport.LocalSpotValue` appends `(mai intunecat)`
(dust, dew, an obstruction) or `(mai luminos)` (a light leak, a reflection), nothing at exactly
zero. `StructureReport.LocalSpotNote` states the threshold read from `Th.LocalSpotWarn`, never
written out, so a recalibration cannot leave the text quoting the old value; both applications
print it. Only tiles with a whole 7x7 neighbourhood are evaluated. The counter-measure for a
misread - checking whether the extreme is round or elongated, since only the round case is dust -
is deliberately not automated.

## Alternatives rejected

- **Making it a sixth test.** The same figure is right for one use and wrong for the other; the
  information to decide is not in the master.
- **A bare signed number.** The first question asked once the figure existed was whether a minus
  meant a bug.
- **Evaluating the border tiles.** At the edge the truncated neighbourhood's median no longer
  follows a gradient: −0.76 % in the corner on a clean 256 px fixture and −1.61 % under a 60 %
  gradient, one step from a false dust warning; a spot on the edge is caught by the worst tile.
- **A shape threshold.** Nobody has calibrated one, and the figure is informative, not a verdict.

## Consequences

- The metric can misread a large-scale feature with a ridge steeper than the 7x7 neighbourhood
  (~250 px on the sensor). Measured on two real masters: on `Nikkor 35mm DX/f2.8-sky/ISO400` the
  extreme is −0.44 % with the five deepest tiles all at `x=117` - a vertical stripe down the
  left edge, not a spot; on the ISO800 master +0.36 % in the top-right corner, the same kind of
  thing. Both a fifth of the threshold, so nothing fired; on a master with a harder edge it could
  warn about dust that is not there, and the reader checks the shape by eye.
- The `--smooth` report reuses `LocalSpotValue(pct, x, y)` for a spot measured on another raster;
  that overload rounds to zero before formatting, since .NET keeps a negative zero (`-+0.00`).
- Ties within 1e-12 keep the first tile in row-major order in both implementations; `crosscheck.py`
  caught a position flip on the diagonal fixture that way.
- The window shows the warning under the verdict, the console under the informative rows.
- Guards: the `structure dust shadow` cross-check scenario (warning and position), the
  `LocalSpot*` half of `StructureReportTests`, `LocalSpots` tests in `StructureAnalyzerTests`.
- Source notes: docs/design/modes-structure.md, docs/design/pipeline.md.
