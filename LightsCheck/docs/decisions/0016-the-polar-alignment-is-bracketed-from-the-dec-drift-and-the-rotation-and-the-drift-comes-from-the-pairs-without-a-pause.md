# 0016. The polar alignment is bracketed from the DEC drift and the field rotation, judged by what it costs in one exposure, and the drift comes from the pairs without a pause

Date: 2026-10-24
Status: accepted 2026-10-24

## Context

The owner asked whether the polar alignment of a session was poor, by how much, and what it
cost, and whether the drift of R-023 is contaminated by the small steps after a pause.
Measured on 2026-10-24 on the Veil session's registration (`docs/validation/2026-10-06-veil/frames.csv`,
the same positions the tool finds within 0.1 px; the scripts are not in the repo):

| | pairs | X (DEC) px/min | Y (RA) px/min | in "/min |
| --- | --- | --- | --- | --- |
| all pairs that were not a move (the tool's rule, V-008) | 45 | -0.274 | -0.476 | -1.6 / -2.9 |
| pairs with no pause above 5 s between the frames | 39 | -0.186 (se 0.019) | -0.961 (se 0.19) | -1.1 / -5.8 |
| the pairs after a pause whose step stayed under 5 px | 9 | -0.518 | +0.368 | -3.1 / +2.2 |

The nine post-pause pairs are presses that moved 3 to 5 px, seven of them on DEC (dX of 2.3
to 4.0 px, the backlash presses of ADR 0007); divided by their longer intervals they halve the
RA drift and inflate the DEC drift. So the drift is contaminated, and `docs/validation.md`'s
sentence that the two figures "are the same slow drift" is wrong: the drift per exposure is
1.9 px, not 1.1, and the walking-noise dash over frames 61-70 is 17 px, not 9.9. The DEC step
of the clean pairs is the steadiest figure of the night (-0.38 +- 0.24 px per 121 s, the same
after RA presses, DEC presses and small presses, -0.31 in the first half and -0.44 in the
second). The RA step is not: -1.94 +- 2.41 px per pair, -0.98 after a press that moved the
mount west, -2.19 after one that moved it east, -2.18 after a DEC-only press, -3.25 after a
press under 5 px (correlation with the press size +0.41, with the pause length +0.05). Every
pair of the night follows a press (40 pairs, 7 single frames), so a steady-state drift was
never observed; Smida (V-013, dithered every frame) has no pair at all.

The field rotation against time is linear at +2.13 arcmin/h (total +8.45' over 3.35 h), with
no curvature (-0.1 +- 8.4 "/h^2) and residuals of 33" rms that come in pairs: each RA dither
turns the tangent plane by its RA step times sin DEC (20 px = 120" x 0.52 = 62"), the
convergence of the hour circles, not the mount. The registration's scale falls by 1.8e-4 over
the night, the refraction compressing the field as it set. Measured again at the review of
2026-10-24: regressing the per-frame rotation on time and on the pointing's RA arc (the WCS of
ADR 0013 on each frame's shift) gives -0.51 rad per radian of eastward arc (-0.59 with the DEC
arc as a second regressor, tan DEC being 0.62) in this frame, drops the residual from 33" to
6-12" rms, and leaves a time term of -0.65 arcmin/h: the 8.45' end to end is mostly the
session's net RA offset of -1237" of arc (the 41 moves' steps sum to -957", the non-move
steps to -280") seen through the hour-circle convergence, not the mount turning.

A mount whose polar axis is off by a small angle e = (e_alt along the meridian, e_az
east-west) tracks about the wrong pole; to first order, with w the sidereal rate, H the hour
angle and d the declination, the stars move in DEC at `w (e_alt sin H - e_az cos H)`, in RA at
`w sin d (e_alt cos H + e_az sin H)`, and the field turns at `w cos d (e_alt cos H + e_az sin
H)`; a tracking-rate error dw adds `dw cos d` in RA and `dw sin d` of rotation and nothing in
DEC. The sum of the squares of the two brackets is `|e|^2` at any hour angle. A forward model
of the exact rotations, projected on the camera with +X north and +Y west as ADR 0013
measured, gives at H = 0 per 10' of each error alone: `e_alt` X -1.08, Y +1.21 "/min, rotation
+1.98'/h; `e_az` X -2.30, Y -0.56 "/min, rotation -0.94'/h; a rate 0.1 % fast Y -0.77 "/min,
rotation +0.47'/h. Fitted to the clean Veil figures, the three unknowns give `|e|` = 6.4'
whatever the hour angle, with the mount 0.67 % fast (6 "/min), which no crystal-driven
tracker is; fitted without a rate error to the DEC drift and the rotation alone, `|e|` =
10.7' and the RA drift it predicts is +1.3 "/min west, against -5.8 "/min east measured. The
DEC drift alone gives `|e|` >= 4.2'. So the frames bracket the polar error between 4' and
11' (4.5' to 6.7' once the dithers' slews are out of the rotation and the three jumps out of
the drift, the figures the tool records) and say that at most 1.3 "/min of the RA drift (the
rotation times tan d) can come from the alignment; the other 4.5 "/min or more is the tracking (its rate, the periodic error of
the worm, flexure, or something a press leaves behind), which these frames cannot separate.
What the alignment costs is then at most 0.57 px of drift in one 120 s exposure at 5.99 "/px
(1.1 "/min on DEC and 1.3 "/min on RA), an elongation of 1.01 on a 3.14 px star (a smear L on
a Gaussian of sigma s reads `sqrt(s^2 + L^2 / 12) / s`); the measured 1.9 px per exposure
reads 1.09, and the good frames' median elongation of 1.19 needs 3 px of smear inside the
exposure. The hand report's reading (section 5, item 7.3) that a better polar alignment would
remove the 0.8 px/min drift and the base elongation is not supported.

Limits the frames do not lift: the split of `|e|` into altitude and azimuth needs the hour
angle, so the site and the camera clock's offset from UT, which the raws do not carry;
refraction moves the apparent field toward the zenith at 0.2 "/min at the start of the Veil
night and 0.8 "/min at its end for a site near Cluj (the field at 60 degrees of altitude
going down to 26, hour angle +2.2 to +5.6 h, parallactic angle about 50 degrees), so a
quarter of the DEC drift at the end could be refraction and the lower bound is that much
soft; the declination for the upper bound comes only from a plate solve.

## Decision

- **The drift of R-023 comes from the pairs without a pause and without a move.** A drift
  pair is two consecutive registered frames with no pause above `Th.DitherPause` between them
  (the frames shot back to back) and no move between them either (a jump of 5 px or more
  between two such frames is the mount's, not the drift: Veil's frames 16, 22 and 54 jumped
  7.3, 5.9 and 9.2 px along RA behind the doubled stars of 17 and 23; the review of
  2026-10-24), and the drift is their mean step per minute, as before. With fewer than
  `Th.DriftMinPairs` (3) such pairs the drift falls back to the pairs that were not a move,
  worded as approximate when that added pairs, so a session dithered every frame keeps its
  walking-noise dash (Smida's `Static` word of ADR 0015 rests on it); the polar diagnosis
  below reads three or more clean pairs only.
- **The polar error is a bracket, not a number.** The lower bound is the DEC component of the
  drift over the sidereal rate; the upper bound adds the tracking's rotation over the rate
  times cos d, with d from the solved FITS at the root (R-025); both at the session's own
  figures, the first-order formulas above, no hour angle. The tracking's rotation is the
  end-to-end rotation with the dithers' convergence taken out: a slew about the mount's RA
  axis turns the frame by its RA step times sin d, so the net slew of the moves (each move's RA
  component minus the drift over its interval) times tan d per radian of arc, in the sense the
  frame's handedness sets (the sign of the determinant of the pixel-to-sky map), is subtracted
  before the rate is taken, and the rate is read only over an hour or more of span, since the
  per-frame rotation scatters by about 10" (the review of 2026-10-24). Without a plate solve,
  the lower bound alone when the trails gave the axis; without an axis, nothing. The RA drift
  is reported beside it with how much of it the alignment can explain (the rotation rate times
  tan d) and that the rest is the tracking.
- **The verdict judges the cost, not the arcminutes.** The drift the alignment can cause in one
  exposure (the DEC drift, and on RA at most the rotation rate times tan d, in quadrature,
  times the median exposure, in pixels; the rotation itself moves no star at the centre)
  against `Th.PolarDriftFwhmFraction` (a third) of the session FWHM, below which the
  elongation it adds stays under 1.03: `OK` when the upper bound's drift is under it,
  `ATENTIE` when the lower bound's drift is over it, `LA LIMITA` between. The rows print the
  bracket in arcminutes, the drift per exposure and the elongation it adds, so the same
  error reads `OK` at 20 mm and `ATENTIE` at a long focal length, the argument of ADR 0015
  against a verdict in arcseconds.
- **Where.** Rows in DRIFT SI ROTATIE (console and window card, no new window work, ADR 0008)
  and one verdict row in REZUMAT.

## Alternatives rejected

- **Keeping the drift over all non-move pairs.** On Veil nine presses of 3 to 5 px halve the
  RA drift and inflate the DEC drift; a drift pair must have had no press in it.
- **Back-to-back pairs only, with no fallback.** Smida has none: its dash and its `Static`
  word would go, against the stack the owner saw (ADR 0015).
- **One number from the three-parameter fit (drift on both axes and rotation).** It needs the
  handedness of the frame on the sky (the WCS matrix and Siril's flip) and it rests on the
  extra RA drift being a rotation about the true pole; on Veil that reads as a tracker 0.67 %
  fast, which it is not, so the number would be exact and wrong. Worth revisiting once a run
  of 20 or more frames without dithering has shown what the RA drift is.
- **The split into altitude and azimuth.** It needs the site and the UT offset as inputs,
  and the azimuth part of it leans on the same RA reading; later, if the owner wants the
  knobs named.
- **Taking the whole net RA offset, drift included, out of the rotation.** Exact under the
  model, it removes a rate error's rotation entirely and leaves the misalignment's as
  `w A cos 2d / cos d`, singular at DEC 45 degrees and three times noisier within 10 degrees
  of it; the dithers' slews alone are taken out, and the drift's own projection stays in the
  tracking's rotation, which the bound reads whole as the alignment's.
- **A verdict in arcminutes against a fixed threshold.** The same 10' is nothing at 20 mm and
  a pixel per exposure at 135 mm; the cost is what the user sees (ADR 0015).
- **Measuring the drift on the plate solves of the raws.** It needs a solver at run time
  (ADR 0013); the registration already gives the positions to 0.3 px.

## Consequences

V-008's drift moves to -0.20 / -0.73 px/min over 36 pairs (the 39 back-to-back pairs measured
above held three mount jumps of 6 to 9 px), its drift per exposure to 1.52 px, its dash to
13.7 px (the word stays `Insufficient`); its polar rows read 4.5' to 6.7' and 0.46 px per
exposure, from a tracking rotation of 1.1'/h once the dithers' net slew is out of the 2.5'/h the
registration saw end to end, against the 4.2' to 10.7' of the measurement above, whose linear
fit at 2.13'/h still carried the slews; V-013 keeps its figures, worded
approximate; `docs/validation.md` gets a dated History line and ADR 0015's "1.1 to 1.3 px per
exposure" a dated note. The polar figures of Veil go to V-008 once measured by the run. The
goldens change on the drift row's wording, the new rows and the summary. The RA drift's
cause stays open: a test run of 20 x 30 s without dithering, which the hand report already
proposed, would measure the steady drift and the periodic error, and then the
three-parameter reading above becomes worth building.
