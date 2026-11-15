# 0059. The limits of what is a star and the halo's rings follow the stars' width

Date: 2026-11-15
Status: accepted 2026-11-15
Supersedes: 0056 in part (the wide stars row kept from 7 px, the limit of area left in pixels and the halo's rings of 3, 5 and 10 px); 0052 in part (the wide stars row); 0006 in part (the limit of a star's area, the limit of a kept width, the halo's rings and a trail that is also a star, reproducing the prototype's)

## Context

ADR 0056 made the stars' width right at any pixel scale and left three sizes in pixels around
it: a component was a star from 5 to 400 px (`Th.MaxStarArea`), the halo was read in rings of
3, 5 and 10 px, and FOCUS carried a `wide stars` row from 7 px that said the brightest stars
were left out (R-088). The owner asked for limits that do not depend on the pixel scale, if a
measurement showed it could be done without letting anything else in among the stars. Measured
on 2026-11-15 with a harness outside the repository that linked the core's sources and replaced
`FrameAnalyzer` by a copy with a switch per variant; it reproduced the six frame cases V-002 to
V-007 and the report of `Veil/2026-10-06-3h` before any variant was read.

**What a star covers.** Drawn stars under the sky noise of a Veil frame, 196 per cell, counted
by the rule of 400 px (the peak in multiples of the sky's noise):

| Width | Profile | 25 | 60 | 200 | 500 |
| --- | --- | ---: | ---: | ---: | ---: |
| 3 px | Gaussian, winged | 196 | 196 | 196 | 196 |
| 5 px | Gaussian | 196 | 196 | 196 | 196 |
| 5 px | winged (Moffat, beta 2.5) | 196 | 196 | 196 | 0 |
| 7 px | Gaussian | 196 | 196 | 196 | 196 |
| 7 px | winged | 196 | 196 | 0 | 0 |
| 10 px | Gaussian | 196 | 196 | 0 | 0 |
| 10 px | winged | 193 | 0 | 0 | 0 |
| 15 px | Gaussian, winged | 0 | 0 | 0 | 0 |

The area a star covers above the detection threshold, over the square of its width as read, is
the same at every width: 3.0 to 3.3 at a peak of 25, 3.8 to 5.2 at 60, 5.2 to 8.7 at 200, 6.2 to
12.9 at 500 (the low figure a Gaussian, the high one a winged star), and 7 to 11 or 22 to 25 for
a star saturated at three times the top of the scale. So 400 px are 64 widths squared for the
2.5 px stars of `Veil/2026-10-06-3h` and 20 for stars 4.5 px wide, about what
`Veil/2026-11-06` (4.7 px) and the first frames of `Milkyway-Core/2026-08-12-Smida` (4.5 px)
have. With a limit of 20 or of 64 widths squared every cell counts
196. In a field of 400 stars of mixed brightness the bright count goes from 25 to 54 at 7 px
(winged), 23 to 71 and 0 to 67 at 10 px, 0 to 73 and 0 to 65 at 15 px.

**A third size in pixels.** A shape was kept up to an FWHM of 15 px (`Th.MaxFwhm`). A star
drawn 15 px wide reads 15.05 px, or 17.4 with wings, so of such stars 68, 23, 0 and 0 of 196
were measured at the four peaks, none with wings, and a frame of them had no width at all.
With the limit at three times the frame's first width all are measured; fields of 20, 25 and
30 px read 20.00, 24.91 and 29.85 px (23.2, 28.6 and 34.0 with wings).

**What lies above 400 px on real frames**, sorted by the harness and checked by eye on sheets
of cut-outs of three frames: 2 to 4 components a frame on `Veil/2026-10-06-3h` (saturated
stars with spikes, a filament of the nebula), 2 to 10 on `Veil/2026-11-06` (saturated stars
with their tail, pieces of nebula, a pair), 27 to 38 on Smida (clumps of stars run together,
Milky Way cloud, satellite trails, the short streaks of geostationary satellites; one true
star and one or two saturated ones). A limit of 64 widths squared lets 22 to 29 of them in on
each Smida frame, nearly all clumps and cloud; being the brightest by flux they take the
stars' places (391 to 372 stars measured, 20 to 28 of the 600 registration stars replaced).
A limit of 20 widths squared, never under 400 px, lets in one component on one frame of
`Veil/2026-10-06-3h`, none on Smida, and 1 to 5 on 34 of the 82 frames of `Veil/2026-11-06`.
A rule that asked each large component for a core near the frame's width dropped 493 true
stars on frame 17 of `Veil/2026-10-06-3h`, whose stars the mount doubled.

**The same real sky at another pixel scale**, since no recorded session has stars wider than
4.7 px: a crop of 2000 x 1400 px of a frame, enlarged 3 times with fresh pixel noise (its noise
is smoother than a real frame's, so only the bright stars are compared). Bright stars of the
frame as it is found again by the rule of 400 px, then with 20 widths squared and three widths:
3 of 162, with no width, against 160 (17.27 px, that is 5.76 at the frame's scale, where the
frame reads 5.73) on `Veil/2026-11-06`; 300 of 541 against 534 on Smida (13.7 px); 951 of 979
against 976 on `Veil/2026-10-06-3h` (7.9 px). New bright objects that were no star at the
frame's scale: 0 to 5 on the Veil frames, 16 to 18 on Smida, mostly stars that part from a
clump when the pixels are finer; the same with 64 and with no limit. No trail was reported on
any of them.

**The halo.** Read on drawn Gaussian stars, plain, then 15 % wider, then with a tenth of their
light in a skirt five widths wide:

| Width | Rings of 3, 5, 10 px | Rings of 1.2, 2, 4 widths |
| --- | --- | --- |
| 3 px | 0.000 / 0.007 / 0.051 | -0.003 / -0.005 / 0.045 |
| 5 px | 0.130 / 0.279 / 0.175 | 0.001 / -0.001 / 0.046 |
| 7 px | 0.652 / 1.095 / 0.686 | -0.005 / -0.003 / 0.041 |
| 10 px | 2.034 / 2.782 / 2.049 | -0.005 / -0.005 / 0.041 |
| 15 px | 4.269 / 4.986 / 4.286 | -0.005 / -0.009 / 0.035 |

Above 3 px the fixed rings read the width again: at 5 px a star 15 % wider reads 0.15 more
halo, and the dew rule (the width up by 15 %, the halo by 0.02) would call a focus drift dew.
On the sessions the halo of the fixed rings follows the frame's width: a correlation of +0.84
over the frames of Smida, +0.69 on `Veil/2026-11-06`, +0.13 on `Veil/2026-10-06-3h`. With rings
of each star's own width: +0.12, -0.19, -0.06. With rings of the frame's one width: +0.66,
-0.03, -0.07; Smida's stars are 5.0 px wide in the centre and 3.3 at the edge, so one width
fits few of them. The dew verdict is `NO` on the three sessions with every variant.

**The width before the stars are known.** The same core measurement run first on the 400
brightest unsaturated components of any area, its median taken, equals the frame's final width
to 0.01 px on the drawn fields and to 0.05 px on Smida (4.50 against 4.45, the clumps in it).
The shapes are kept, so nothing is measured twice: 33 to 64 ms after the detection became 29
to 85 ms, at most 32 ms more on a frame of about 3 s. A width read in one pass from each
component's area and peak was off: 4.20 for 2.84 px on a frame of doubled stars, 6.03 for
4.49, 5 to 8 % high on winged stars.

**Trails.** No trail was reported among drawn stars of 3 to 30 px or on the enlarged frames,
and the drawn trail was found each time. A trail of 251 to 400 px was also a star (CR-013 of
`docs/review/2026-11-13-code-review.md`): two on each of frames 1, 5 and 23 of Smida and one
on frame 3. A
larger limit of area would widen that.

## Decision

A component is a star when it is no trail and its area is at least `Th.MinStarArea` (5 px) and
at most `Th.MaxStarAreaWidths` (20) times the square of the frame's first width, never under
`Th.MaxStarAreaFloor` (400 px). A shape is kept up to `Th.MaxFwhmWidths` (3) times the first
width, never under `Th.MaxFwhmFloor` (15 px). The first width is the median core width of the
400 brightest unsaturated components of 5 px or more that are no trail, whatever their area
(`FrameAnalyzer.FirstWidth`); a frame without one keeps the two floors. The halo is the flux in the ring from `Th.HaloInnerWidths` (2) to
`Th.HaloOuterWidths` (4) of each star's own core width over the flux within
`Th.HaloCoreWidths` (1.2) of it. The `wide stars` row of FOCUS, R-088 and `Th.WideStarFwhm`
are gone. The owner chose this of the options put to them on 2026-11-15 ("Da, recomandarea").

## Alternatives rejected

- **No floors: 20 widths squared and three widths alone.** No size in pixels is left, and on
  the recorded sessions many figures move for no measured gain: `Veil/2026-10-06-3h` loses
  about 13 saturated stars a frame (up to 61), frame 17's 90th percentile of width goes from
  7.51 to 7.40 px, the rotation from 8.5' to 8.4', Smida's width from 3.99 to 3.98 px.
- **64 widths squared, what 400 px are to stars 2.5 px wide.** Lets the clumps and the cloud
  of Smida in (above), and a drawn knot six widths across; a cell of V-010 becomes
  `13.28 px = 81.1"`.
- **A limit per component from its own peak, or a core near the frame's width.** The first
  moved Smida's counts by 30 a frame without keeping anything the simple limit lets in; the
  second drops the stars of a doubled frame.
- **Rings of the frame's width.** One ring set a frame, and a halo that still follows the
  width where the lens draws wider stars in the centre.
- **A smallest area that follows the width.** What it keeps out are hot pixels and specks of
  noise, which are pixels at any scale: a frame of noise alone gives three components of 1 px
  in 9 megapixels, and the faint drawn stars are found alike at every width.
- **Keeping the row for stars wider than were measured.** Nothing measured stays unread up to
  30 px, a width no sky draws at these focal lengths.

## Consequences

Stars of 3 to 30 px are counted, measured and kept for registration alike, and the halo means
the same at every width, so the dew verdict no longer reads a change of focus on wide stars.
Under 4.5 px of width a frame's star list is what it was, less its trails. Every halo figure
moves: the first and last good frames read 0.025 / 0.012 to 0.020 / 0.007 on
`Veil/2026-10-06-3h`, 0.086 / 0.057 to 0.042 / 0.036 on Smida, 0.135 / 0.112 to 0.031 / 0.008
on `Veil/2026-11-06`; no class and no verdict changes (`docs/validation.md`, 2026-11-15, holds
the drift per case). From about 20 px a winged star's skirt breaks into specks that are
counted (the star count 30 to 170 % high at 20 to 30 px); the width stays right. A star with
wings saturated at three times the top of the scale covers 22 to 25 widths squared, so above
4.5 px the most saturated stars are still no stars, as before. Two floors in pixels remain, by
choice.

The first width is a middle value. Bright patches that are no stars leave it alone while they
are fewer than the bright stars, and set the limits for themselves when they are as many: a
frame of 4 stars 3 px wide with 2, 4 and 6 round patches of 30 px reads 3.22, 16.48 and
29.77 px (the probe of the review of the branch, 2026-11-15), where the limit of 400 px read
3.22 each time. It takes a frame with very few stars over 20 sigma and several bright patches,
which none of the recorded frames is near. A star nearer the border than four of its widths
has a shape and no halo, and is not replaced among the 40; the band is 0.4 widths less 2 px
wide.

The rule that tells a trail is the one it was (R-015): a streak close to a row or a column
fills its box and is not called a trail. Among stars under 4.5 px such a streak over 400 px
is neither trail nor star, as before; among wider stars the larger limit of area makes it a
star (a streak 90 px long, of 667 px, within 10 degrees of a row, among stars 10 px wide: the
probe of the second review, 2026-11-15). It is counted and kept for registration, one
component among thousands; telling such a streak is a change to R-015, not made here. The frame report's halo line names the rings in star widths. Guarded
by `R014_BrightWideStarsAreCountedBrightAndMeasuredAtTheirWidth`,
`R014_AKnotOfNebulaAmongWideStarsIsNotAStar`, `R014_TheHaloReadsTheSameOnNarrowAndOnWideStars`,
`R014_HaloInRingsOfTheStarsOwnWidthIsNearZeroAtAnyWidth`,
`R014_ASessionOfWideStarsIsReadAtItsWidthWithEveryStarCounted`,
`R014_UnderFourAndAHalfPixelsTheLimitsAreThoseInPixels`, `R014_StarsWithABroadSkirtAreCounted`,
`R014_AFrameWithoutAnUnsaturatedBrightStarKeepsTheLimitInPixels`,
`R014_AFewBrightPatchesDoNotSetTheStarsWidth`, `R014_TheHaloIsReadInRingsOfEachStarsOwnWidth`,
`R014_AShapeWiderThanTheLimitOfWidthIsNotKept`, `R015_AShortThinStreakIsATrailAndNotAStar`
and `R015_ABrightStreakAmongWideStarsIsATrailAndNotAStar`.
