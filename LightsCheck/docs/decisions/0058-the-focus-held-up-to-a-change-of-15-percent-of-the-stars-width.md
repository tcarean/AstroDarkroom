# 0058. The focus held up to a change of 15 % of the stars' width

Date: 2026-11-15
Status: accepted 2026-11-15

## Context

FOCUS says the focus held when the stars' width moved by less than 10 % between the first and
the last ten good frames (R-023, `Th.FocusHeldFraction`), a limit set on the widths of the
first versions' moments. With the widths of ADR 0056 the Smida session
(`Milkyway-Core/2026-08-12-Smida`, 32 frames at 20 mm) reads 4.24 px against 3.79 px, -10.4 %,
and its verdict turned from `GOOD` to `WARNING ... the focus changed`; the old widths read
4.34 against 4.04 px, -7 %. The owner said the picture of that session came out very well,
printed large too, and asked how much the change matters. Measured on 2026-11-15:

- The width falls steadily, 0.02 px a frame, from 4.46 px on the first frame to 3.72 px on
  the last, the centre (5.5 to 4.7 px) and the edge (3.7 to 3.1 px) alike, by 15 %.
- The stack of the average star, each frame a Gaussian of its own width: 3.98 px with all 32
  frames, against 3.78 px had the whole session been as the last ten: the drift costs 5 % of
  the width, 0.2 px. Leaving the first 10 frames out gains 2.2 % of width for 21 % more noise;
  the first 16, 3.4 % for 41 %.
- The stars of the owner's stack of the 32 (`result_3813s.fit`) are no wider than the frames'
  average: 4.7 px in the centre and 3.1 px at the edge, against 5.0 and 3.3 px, on another
  set of stars.
- A steady drift whose ends differ by 10 % stacks 4.8 % wider than its sharp end; by 15 %,
  7.0 %; by 20 %, 9.1 %.

## Decision

`Th.FocusHeldFraction` is 0.15. It is what the old 0.10 meant on the one recorded session
with a drift, whose 10.4 % the old widths read as 7 %, and that session, good in print, holds
its focus as it did.

## Alternatives rejected

- **Keep 10 %.** The only real session that trips it lost 5 % of star width to the drift and
  would have lost a fifth of its signal-to-noise to the cure.
- **20 %**, where the stack is about a tenth wider than its sharp end. No session measured
  says that a tenth is where it starts to show; 15 % rests on one that says 10 % does not.

## Consequences

The Smida session reads `the focus held` again (V-013 keeps `focusHeld`). A session whose
width moves by 10 to 15 % between its ends no longer gets the warning; its stack is up to 7 %
wider than its sharpest frames. The dew test's rise of 15 % (`Th.DewFwhmRise`) and the soft
frame's 1.3 times (`Th.SoftFwhmFactor`) were set on the old widths too and now bind a little
sooner than they did; no recorded verdict moved with them, and they stay. Guarded by
`R023_TheFocusHeldUpToAChangeOfFifteenPercent`.
