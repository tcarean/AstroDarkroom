# 0015. The walking-noise verdict reads the drift per exposure, and the sensor's pattern comes from the lights' median

Date: 2026-10-10
Status: accepted 2026-10-10

## Context

The owner compared two stacks on 2026-10-10: the Veil of 2026-10-06 (135 mm, 87 frames, dithered
every two frames, worms visible in the stack) and the Milky Way at Smida of 2026-08-12 (20 mm,
32 frames, dithered every frame, a clean stack after one hour). LightsCheck called the dithering
`INSUFICIENT pentru walking noise` on both, and the Smida map looked worse than the Veil's,
which contradicted the stacks. Measured that day (the scripts are not in the repo; the figures
are in the plan `docs/plans/2026-10-10-judge-walking-noise-by-the-drift-and-measure-the-sensor-pattern.md`):

| | Veil | Smida |
| --- | --- | --- |
| scale | 5.99 "/px | 40.4 "/px |
| drift, arcseconds | -1.6 / -2.9 "/min | -2.2 / -2.8 "/min |
| drift per 2 min exposure | 1.1 to 1.3 px | 0.15 px |
| longest run at 10 px | 10 frames (61-70) | 12 frames (9-20) |
| dash = drift per exposure x (frames of the longest stretch at one spot - 1) | ~11 px | ~1.9 px |
| darks in the session | none | 20, a master dark subtracted |
| warm pixels in the calibrated frames above 3 sigma of one frame | 1162 / Mpx | the master dark removed held 34 / Mpx |
| autocorrelation anisotropy of the stack's background, G, lags 3 to 48 | 1.45 along Y = RA | 1.15 along X |

(Corrected 2026-10-24: the Veil drift in the table was read over 45 pairs that include nine
post-pause presses of 3 to 5 px; over the 39 pairs without a pause it is -1.1 / -5.8 "/min,
1.96 px per exposure and a dash of 17.7 px, and the mount did not drift the same on both
nights (ADR 0016). The verdicts and the threshold of 3 px stand.)

The mount drifted the same on both nights. At 20 mm that drift is a seventh of a pixel per
exposure, so the sensor's pattern does not move between the frames that share a spot: it stacks
as points, which the master dark had removed anyway. At 135 mm it is over a pixel per exposure,
nothing removed the warm pixels of ISO 1600 (no darks), and inside every run of frames at one
spot they became dashes along RA, the worms the owner saw. The verdict of ADR 0007 reads the
positions alone and cannot tell these two sessions apart.

## Decision

- **The walking-noise verdict reads the drift.** `SeriesAnalyzer` adds the drift over one
  exposure (the drift vector of R-023 times the median exposure), the dash it draws across the
  longest stretch of two or more consecutive frames within `Th.CoverageLoose` of its first
  (drift per exposure times the frames minus one; a stretch of two counts, since a pair with a
  large drift between them draws a dash too, which the runs of three of ADR 0007 missed, the
  review of 2026-10-10; the measured span of a stretch was tried and rejected, since a dither
  of 9 px inside the stretch is two dots, not a dash, and the registration's 0.5 px wobble
  reads as drift), and a three-way verdict: `Ok` by the dither rule of ADR 0007, unchanged; else `Static` when the dash is
  under `Th.WalkingStreakVisible`; else `Insufficient`. The threshold is 3 px, a rule of thumb
  placed between Smida's 1.9 px (nothing visible) and Veil's 11 px (worms), to tune on more
  sessions. The dither rule stays as it was, so a well-dithered session is still `OK` without
  any drift pair to read. The two verdict words are printed worst first, so the chip's colour
  follows the worse one.
- **The sensor's pattern is read from the lights' median.** Every measured light keeps a 512 px
  central crop of its raw mosaic; the per-pixel median across the unregistered crops keeps what
  did not move with the dither, and the high-pass of that median per CFA position gives the
  pattern's sigma (the median's own noise, measured from the medians of the odd and the even frames, taken out in quadrature), reported in ADU and as a
  fraction of the frames' own high-pass sigma, plus the hot pixels (10 sigma) per megapixel.
  Not a count of warm pixels at 3 sigma: measured on the Veil raws, that count sat on the
  pattern's steep tail (135 green pixels above 53 ADU, 12 above 66 ADU in 127 k), so a 20 %
  change of the noise estimate between a MAD and a clipped standard deviation moved it from
  575 to 126 per megapixel, and the debayered green channel of the calibrated frames gave 1162
  from the same sensor; the sigma (2.1 ADU on green, 10 % of a frame's noise) moves with
  none of that. The median is read only with 7 frames or more and while the largest cluster
  of frames at 3 px holds under half the registered frames, so no pixel sees a star in half
  the frames. The raws carry the pattern whether or not the session has darks, so the figure
  says what the sensor does; the `darks/` folder, counted and never read, says whether Siril
  will subtract it.
- **How much of the pattern the stack keeps.** From the registered positions alone: every
  frame adds its copy to the mean and every pair of frames within 1.5 px adds the same pixels
  twice more, `(n + 2 pairs) / n^2`, against the ideal `1 / n`. On Veil 1.6 % against 1.1 %;
  on Smida 23 % against 3.1 %, the number that said the Smida map was not wrong, only
  harmless at that drift.
- **The words.** DRIFT SI ROTATIE gains `drift intr-o expunere`; DITHERING gains `scara` (what a
  move is in arcseconds at this focal length), `pattern in stiva` and `pattern fix`; the Dithering tab shows an
  explanation of walking noise, the focal length, what breaks it and how to take darks without
  touching the lens, worded in the core like the Respinse tab's (ADR 0012).

## Alternatives rejected

- **Measuring the walking noise on the stack (`result.fit`), as the autocorrelation anisotropy
  of the background along the drift axis.** Measured on both stacks: 1.45 on Veil against 1.15 on
  Smida, a weak discrimination, and confounded on Veil by the stars' own elongation along RA
  (1.3 on the good frames, 2.3 on the rejected), which no star mask could remove on a field that
  dense without masking over 80 % of the image. The stack also arrives after the session; the
  lights already say whether the pattern will walk. Kept out until a better discriminator exists.
- **Reading the darks to measure the pattern.** A session without darks (Veil) is exactly the
  one that needs the figure, and the lights carry the pattern anyway.
- **A dither verdict in arcseconds.** The sensor sees pixels; the same move is 13 px at 135 mm and
  2 px at 20 mm, and the dash is a count of pixels. The `scara` row translates instead.
- **Rejecting the frames of a run.** ADR 0011: dithering is a property of the stack, not a defect
  of the frame.
- **A larger crop, or the whole frame, for the pattern.** 1024 px as floats is 4 MB per frame, 800
  MB on a 200-frame session beside the parallel decode; 512 px gives 262 k pixels, enough to count
  1000 warm pixels per megapixel to a few percent, at 1 MB per frame.

## Consequences

`WalkingNoiseOk` of `DitherAnalysis` stays the dither-only rule and the validation figure; the
series' `WalkingNoise` is the word printed. The goldens change on the `darks` header line, the
`drift intr-o expunere`, `scara`, `pattern in stiva` and `pattern fix` rows and the verdict
words. R-042 to R-048 pin the rules on the fixture, which gained `darks:` and a synthetic
sensor pattern; V-012 and V-013 add the Smida session beside Veil's cases. The pattern sigmas
and the surviving fraction of both sessions are measured by the run and recorded in
`docs/validation.md`; the 1162 / Mpx above was on Siril's calibrated, debayered frames and is
the comparison that showed the count fragile, not a figure.
