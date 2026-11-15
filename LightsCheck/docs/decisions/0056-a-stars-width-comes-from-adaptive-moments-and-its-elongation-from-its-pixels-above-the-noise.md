# 0056. A star's width comes from adaptive moments and its elongation from its pixels above the noise

Date: 2026-11-15
Status: accepted 2026-11-15; the wide stars row kept from 7 px, the limit of area left in pixels and the halo's rings of 3, 5 and 10 px superseded by 0059
Supersedes: 0052 in part (the moment window of 6 px, and the limit and the words of the wide stars row); 0006 in part (the star shapes, FWHM, elongation and angle, reproducing the prototype's moments)

## Context

A star's width, elongation and angle came from second moments in a fixed circle of 6 px, on
values cut at zero (R-014). The 6 was the default `rad=6` of `prototype/analyze.py`, written
for stars of about 3 px at 135 mm and never compared with anything else. ADR 0052 kept it,
having shown that with those moments a wider or a star-following circle reads the sky's noise
and the neighbours, and said the limit in a `wide stars` row from 4.5 px. The owner then asked
for a width that is right at any focal length and pixel scale, chosen after a measurement
(plan `docs/plans/2026-11-14-measure-the-star-width-right-at-any-pixel-scale.md`).

Measured on 2026-11-14 and 15 with a harness outside the repository that compiled the core's
sources and swapped `StarShapes.Measure`, so every candidate read the same stars the tool picks
on the same residual raster. The candidates: **O** the moments of ADR 0052; **A** moments not
cut at zero, with the local sky from a ring, in a circle of 1.5 times the width read; **B**
adaptive moments, the second moments under a Gaussian weight that takes the star's size and
shape, repeated until weight and star agree; **C** the width at half height from the radial
profile; **D** a fit of a 2D elliptical Gaussian with its own background; **DM** the same with
a Moffat profile.

Synthetic Gaussians drawn by integrating each pixel, 196 per cell, under the noise of a Veil
frame (sigma 14 ADU), through the tool's own detection with its limit of area lifted, so that
the wide stars reach the estimators (the limit is the subject of the last paragraph of this
section); the median reading against the drawn width, in percent, at a peak of 25 / 60 / 200
sigma:

| Width | O | A | B | C | D | DM |
| ---: | --- | --- | --- | --- | --- | --- |
| 2 px | +111 / +65 / +27 | +6 / +5 / +5 | +5 / +5 / +5 | +9 / +10 / +10 | +5 / +6 / +6 | +4 / +4 / +5 |
| 3 px | +30 / +14 / +5 | +3 / +2 / +2 | +3 / +2 / +2 | +4 / +4 / +5 | +2 / +3 / +3 | +1 / +2 / +2 |
| 5 px | -3 / -3 / -3 | +1 / 0 / 0 | +1 / +1 / +1 | +2 / +2 / +3 | +1 / +1 / +1 | 0 / 0 / 0 |
| 7 px | -17 | 0 | 0 | +1 to +2 | 0 | 0 |
| 10 px | -35 | -1 | 0 | +1 to +2 | 0 | -1 |
| 15 px | -54 | -1 | 0 | +1 | 0 | -1 |

O is wrong under 4 px as well: the noise cut at zero counts as flux all over the circle, so
the same star reads wider when it is fainter; the `3.00 at 3 px` of ADR 0052 was a star
without noise. Under 4 px every candidate reads 2 to 5 % wide, the pixel's own width (a
Gaussian of 2 px integrated over the pixels is 2.11 px wide, one of 3 px 3.07). The spread
from star to star at 3 px and 25 sigma, in percent of the width: O 4.5, A 5.6, B 1.9, C 4.4,
D 2.7, DM 2.4.

Stars with wings (a Moffat of beta 2.5), against the width at half height, at every width
from 4 px up: B +16 %, D +7 %, DM +1 %, C +4 to +12 % with the star's brightness, A +40 %; O
+42 % at 3 px and -35 % at 10 px. With beta 4: B +10 %, D +5 %.

Stars of a known width added into two real frames (frame 4 of `Veil/2026-10-06-3h`,
`DSC_0020` of `Milkyway-Core/2026-08-12-Smida`; 300 per width, peaks 25 to 200 sigma, at
random places among the real stars) and read back: B and D within 1 to 2 % from star to star
and the same for the faint and the bright half; O +25 % on the faint half and +13 % on the
bright one at 3 px, -35 % at 10 px. Every candidate reads a 3 px star 9 % wide there and a
10 px star 1 % wide: the red and blue places of the mosaic are filled from the green
neighbours before the stars are measured.

The median width of the same 400 stars of ten raws:

| Frame | O | B | D |
| --- | ---: | ---: | ---: |
| `Veil/2026-10-06-3h`, frames 4, 72, 83 (clean) | 3.06, 3.03, 3.06 | 2.51, 2.48, 2.48 | 2.45, 2.43, 2.42 |
| the same, frame 1 (trailed) | 3.85 | 4.12 | 4.12 |
| the same, frames 17, 23 (stars doubled by a jump of the mount) | 4.92, 4.88 | 2.84, 3.04 | 2.69, 2.86 |
| `Veil/2026-11-06`, `DSC_0001`, `DSC_0050` | 4.62, 4.71 | 4.49, 4.75 | 4.20, 4.57 |
| `Milkyway-Core/2026-08-12-Smida`, `DSC_0002`, `DSC_0020` | 4.46, 4.10 | 4.46, 3.96 | 4.37, 3.85 |

The time for the 400 stars of a frame (a frame takes about 4.7 s in all): O 2 ms, B 7 to 40,
C 16 to 40, D 80 to 290, DM 800 to 2800.

Elongation, on Gaussians drawn 1.3, 1.6 and 2.0 times as long as wide: O reads 1.22, 1.46,
1.75 at 3 px and 1.07, 1.10, 1.13 at 10 px; B and D read 1.29, 1.58, 1.97 at every width, the
angle within a degree. But B and D follow one core of a doubled star: on frames 17, 22, 54
and 60 of `Veil/2026-10-06-3h`, where the mount jumped during the exposure and every star has
a second copy 6 to 9 px away, B reads 1.53, 1.25, 1.44 and 1.40 where O read 2.29, 1.77, 1.71
and 1.77, since its fixed circle held part of the copy. Two ways of reading the whole star in
a circle that follows the core's width were tried. Moments not cut at zero, with a ring sky,
read round faint stars as long (1.34 at 3 px and 25 sigma). The moments of the pixels at
least 3 local sigmas above the local sky, in a circle of 2.4 core widths (what 6 px was to the
2.5 px stars it was written on), read round stars 1.07, 1.04 and 1.01 at 25, 60 and 200 sigma,
the drawn ratios 1.29, 1.57 and 1.95 at 3, 5 and 10 px, a doubled star at the elongation of
its second moments, and those four frames 2.49, 1.99, 2.06 and 1.99.

Not the estimator's: the detection calls a component of more than `Th.MaxStarArea` (400 px)
no star. Of 196 drawn stars per cell, all pass up to 7 px Gaussians; at 10 px they pass at a
peak of 25 sigma and none at 60 or 200; at 15 px none; with wings, at 7 px, 184 pass at 60
sigma and none at 200.

## Decision

`StarShapes.Measure` reads each star twice (R-014). The core, by adaptive moments (B): its
centre, flux, width and the elongation of the core. The whole star, by the moments of the
pixels at least `Th.WholeStarSigmas` (3) local sigmas above the local sky inside a circle of
`Th.WholeStarRadiusWidths` (2.4) core widths: the elongation and the angle the frame reports.
No constant of the measurement is a size the star must have. The frame keeps the median
elongation of the cores beside that of the whole stars (`FrameMeasurement.CoreElongationMedian`);
ADR 0057 says what the classes make of the two.

The `wide stars` row of FOCUS stays, for what is still not measured: from a session width
above `Th.WideStarFwhm`, now 7 px, it says that the brightest stars are left out (R-088). The
limit of area is a task of its own.

The prototype stays frozen; the frame figures of the C# no longer equal its `fwhm` and `elong`
columns.

## Alternatives rejected

- **The Gaussian fit (D).** As right as B on Gaussians and half as far off on winged stars,
  and its width is the kind Siril prints; ten times slower, more code (a solver), a little
  more spread on faint stars, and blinder than B to a second copy (a star doubled 5 px apart,
  the copy half as bright, reads 1.14).
- **The Moffat fit (DM).** The only one right on winged stars; 1 to 3 s a frame, its beta sits
  at a bound on most real stars, and its spread on real frames is the largest.
- **The width at half height (C).** Twice the spread of B, a reading that moves with the
  star's brightness on winged stars, and 5 to 17 % low on elongated stars.
- **Moments not cut at zero (A).** Read the wings (+40 %) and the noise (failures on a fifth
  of the faint stars with a circle of twice the width).
- **B alone, its own elongation for the frame.** Simplest; the frames with doubled stars
  would read as good ones.
- **Only the width replaced, the elongation left in the circle of 6 px.** No elongation
  figure would move; the elongation would stay tied to the pixel scale (a 10 px star twice as
  long as wide reads 1.13, and at 4.7 px the circle does not reach a tail).

## Consequences

Every recorded FWHM moves, the clean frames of the 135 mm sessions from about 3.06 to 2.5 px,
and every elongation; `docs/validation.md` has the drift table of 2026-11-15 and what the
owner re-recorded. The width of a star with wings reads 10 to 16 % above its width at half
height, the same at every scale, so widths compare across sessions and focal lengths but sit a
little above what a fit prints. A star doubled closer than about twice its width reads as one
stretched core. The halo keeps its rings of 3, 5 and 10 px, and the detection its area limits:
a session of stars wider than about 7 px is measured on fewer and fainter stars, and says so.
A frame's shapes cost 20 to 70 ms instead of 2. `Th.ElongatedRatio` (1.3, the share of
stars the frame reports as elongated) and `Th.AngleVoteRatio` (1.2) stay as they were, and the
figures they give move with the elongation: a clean frame of the 135 mm session has 20 % of
its stars above 1.3 where it had 14 %. A star needs the whole star's circle and its ring
inside the raster, about 15 px from the edge at 3 px of width and 40 px at 10 px, where 8 px
were enough. Guarded by
`R014_AStarIsReadAtItsWidthWhateverItsWidthInPixels`,
`R014_AFaintStarAndABrightOneOfTheSameWidthReadTheSame`,
`R014_AWideElongatedStarGivesItsRatioAndAngle`,
`R014_ADoubledStarReadsASharpCoreAndALongWholeStar`,
`R088_ASessionOfWideStarsSaysTheBrightestAreLeftOut` and
`R088_ASessionOfStarsFivePixelsWideIsReadAtItsWidthAndHasNoRow`.
