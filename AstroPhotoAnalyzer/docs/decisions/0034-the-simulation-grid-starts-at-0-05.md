# 0034. The simulation grid starts at 0.05

Date: 2026-10-06
Status: accepted 2026-10-06
Supersedes: 0028 in part (the grid's floor of 0.10 noted in its consequences)

## Context

The simulation table of Anexa B lifted the measured background to every target of
`Parameters.SimulationTargets`, 0.10 to 0.20 in steps of 0.01. The floor of 0.10 was the
level whose L\* was the paper black of the old baryta figure (Dmax 2.0, L\* 9). Since ADR 0028
baryta is Dmax 2.4 (paper black L\* 3.6) and the dust of a dark image reaches it under 0.10:
the exact target is 0.093 on `de_printat.png` and 0.067 on `Rosetta_starless_no_WCS.fit`,
while the ladder the exact target is interpolated on already started at `ExactTargetLo`
(0.05). The rows between 0.05 and 0.09, where the dust comes onto paper black, were missing
from the table a reader looks at, and the recommendation could stop at 0.10 with the sentence
"grila nu coboara mai jos". ADR 0028 noted the floor as a leftover of the old paper.

## Decision

The grid is 0.05 to 0.20 in steps of 0.01, sixteen targets; the ladder of the exact target,
the explicit `--target-bg` row and the recommendation rule (the lowest print-safe row, ADR
0029) are unchanged. The owner asked for it on 2026-10-06, testing the window on the new
images.

## Alternatives rejected

- **Keep 0.10 and rely on the exact-target line.** The line gives the number, the table shows
  the rows; a reader wants to see the row where the dust lands on black, with its sky L\* and
  its share of pure black.
- **Start the grid at `ExactTargetLo` by construction.** The two are different things, a
  display grid and a measurement ladder; they coincide at 0.05 today and may not later.

## Consequences

- `Parameters.DefaultSimulationTargets` gives sixteen values; the help text, the README, R-023
  and R-043 say 0.05..0.20; the golden tables have five more rows each; the Tonuri tab's
  guide and the "Fundal-tinta" help interpolate the bounds and follow. The recommended rows of
  the validation cases did not move (the visibility rule, not paper black, sets them on the
  reference images).
