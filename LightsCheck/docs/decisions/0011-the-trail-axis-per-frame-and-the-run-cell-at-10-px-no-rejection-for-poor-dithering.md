# 0011. Each trailed frame names its trail's axis, each frame its run at 10 px; poor dithering rejects nothing

Date: 2026-10-09
Status: accepted 2026-10-09

## Context

Reading the RESPINSE list of the Veil session, the owner asked what `dara la -90 grade` meant
and on which mount axis the trail was, and whether the frames shot without an effective dither
(the runs of consecutive frames at the same spot, which make the walking noise) should count
against a frame too. The series already knows the RA axis (R-025: the WCS of a plate-solved
FITS, else the trail direction) and every frame its trail angle and coherence (R-013); the
two were never put together per frame. Measured on the Veil session (`frames.csv` of the hand
analysis): the 22 bad frames have angles within 13 degrees of vertical at coherence 0.94 or
more, the 17 marginal within 17 degrees at 0.85 or more, while the good frames' coherence runs
from 0.12 to 0.90. The runs of 3 or more consecutive frames at one spot: none at 3 px, 8 at 5
px (8 frames beyond the third of a run), 11 at 10 px (20 frames beyond the third, the longest
61 to 70). Rejecting those 20 would cut 30 % of the kept signal and still leave runs of 3; in a
stack of the 65 kept Veil frames with 135 well-dithered frames from later nights, the Veil
streaks keep their shape at a third of the amplitude, about half the visibility against the
random noise, which falls too.

## Decision

A trailing flag, bad or marginal, carries the axis of its trail: `pe RA`, `pe DEC` or `oblica`,
the angle within `Th.TrailAxisTolerance` (25 degrees) of the RA axis, of the other axis, or of
neither; given only when the RA axis is known and the frame's coherence is above the existing
`Th.TrailAxisCoherence` (0.8), the threshold the series already uses to pick the frames that
vote for the axis. The marginal flag gains the angle it lacked. Every registered frame names
its run at `Th.CoverageLoose` (10 px), the radius of the walking-noise verdict, as a cell
`k/n de la f` of the frame table, three CSV columns and a ring on the dithering map. A run
changes neither the class nor the flags of a frame: dithering is a property of the stack, not
a defect of the frame, and the cure is more well-dithered frames, not fewer. The explanation of
the words (elongation, trail, RA against DEC, where the marginal frames are) is a text of
`Report/` shown by the window's Respinse tab and repeated in `README.md`.

## Alternatives rejected

- **A marginal class for the frames beyond the third of a run at 10 px.** On Veil it would
  turn 20 more frames yellow for something that is not a defect of theirs; the owner chose the
  cell without a class change.
- **Rejecting the frames of a run.** Removing a frame lowers nothing but the signal; the
  streaks of the remaining frames keep their shape, with a larger share of the stack.
- **A looser coherence threshold for the word than for the axis vote.** The Veil frames with a
  flag all sit above 0.8 and the good ones reach 0.9: a new constant would decide nothing on
  the data at hand and add a second threshold to tune.
- **The axis word on the bad frames only.** The marginal frames on Veil are as coherent and as
  vertical as the bad ones; the word costs nothing and the owner reads both lists.
- **The run cell at 5 px.** The 5 px runs are the hot-pixel question, which the verdict already
  answers (fine on Veil); the frames the owner wanted to see are the walking-noise ones.

## Consequences

The reason of a trailed frame changes wording (`dara pe RA (-90 grade)`), so the two series
goldens change on those lines and on the new table column; `R033` and `R034` pin the rules on
hand-made patterns and the fixture, V-009 the words on the Veil session. A later change of the
RA-axis rule (R-025) moves the words of every frame with it, since the per-frame axis is derived
from the series' axis, not measured again.
