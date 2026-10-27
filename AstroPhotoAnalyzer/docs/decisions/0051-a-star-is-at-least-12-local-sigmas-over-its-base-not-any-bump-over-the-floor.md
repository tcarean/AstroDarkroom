# 0051. A star is at least 12 local sigmas over its base, not any bump over the floor

Date: 2026-10-27
Status: accepted 2026-10-27
Supersedes: 0016 in part (the amplitude floor alone decided a star)

## Context

The star rule of ADR 0016 and R-070 kept any window maximum whose amplitude over the border
median reached 3000/65535 (0.046) and whose half-widths fell in 0.8..15 px. On the starless
reference FITS `starless_rbf0.5_stretch0.17.fit` it kept 6297 "stars" at a median FWHM of 2.38 px,
recorded in V-004 on 2026-10-02 as "every other figure of the run" without anyone reading the
file's name; the Veil starless frame gave 22 485, the Rosetta starless 2858. The owner noticed on
2026-10-27 ("imaginea nu are stele... Vezi 6297 de stele. Cum? De unde?"). A Python replica of
the rule reproduced the 6297 exactly and showed what they are: the grain of the stretched noise
and knots of nebulosity. Their median amplitude is 0.058, 3.2 local sigmas; the floor sits on the
expected maximum of 625 noise samples (3.1 sigmas of the lag-3 noise, 0.044), so a share of the
grain passes by itself, more of it in the bright band where the stretched noise is larger. The
README's promise that a starless image is judged by the dpi rule (R-074) was therefore never
kept on a noisy frame, and the size table judged the grain's width as the real detail.

Measured on 2026-10-27 on the aligned pair `Rosetta_withStars_no_WCS.fit` /
`Rosetta_starless_no_WCS.fit` (best shift 0,0; 96.9 % of the pixels differ by under 0.005). A
detection on the frame with stars is a star when the difference image at its peak explains at
least half its amplitude: 1218 stars and 2181 grain among 3399 detections. Candidate criteria
per detection, the amplitude against a sigma:

| criterion | stars | grain |
|---|---|---|
| amplitude / local sigma (1.4826 x MAD of the 96 border pixels about their median) | median 12.5, p10 4.6 | median 3.2, p90 4.3, max 12.2 |
| amplitude / global sigma (MAD of the neighbour differences over the frame) | at 10: 876 kept | at 10: 69 kept |
| flux of the 5 x 5 core / (5 x local sigma) | at 10: 907 kept | at 10: 110 kept |
| roundness (min / max of the two half-widths) | p10 0.57 | median 0.73: no separation |

"Stars" left on the starless frames by the local-sigma criterion at k = 8 / 10 / 12 / 15, the
limit under which the FWHM is not reported being `StarMinCount` = 20:

| frame | now | 8 | 10 | 12 | 15 |
|---|---|---|---|---|---|
| `starless_rbf0.5_stretch0.17.fit` | 6297 | 42 | 9 | 2 | 0 |
| `starless_rbf0.5_stretch0.1.fit` | 1551 | 52 | 11 | 5 | 0 |
| `Rosetta_starless_no_WCS.fit` | 2858 | 2 | 1 | 1 | 0 |
| `Veil_Starless.fit` | 22 485 | 2 | 0 | 0 | 0 |
| `Andromeda_starless_WithWCS.fit` | 10 | 2 | 0 | 0 | 0 |
| `Orion_with_WCS.fit` | 276 | 0 | 0 | 0 | 0 |

On the frames with stars at k = 12: Rosetta keeps 621 stars and 1 grain (median FWHM 2.14
against 2.37 over all labelled stars: the faint ones measure wider on noise); `de_printat.png`
(read as 8 bits by the probe) 5484 of 8395 with the median from 3.18 to 3.10; Andromeda 3037 of
3526 (5.30 to 5.44); Orion 1372 of 3901 (3.51 to 4.12: the grain at 2 px pulled the median down,
the stars are near 4 px). The tool itself, on 2026-10-27 at 12 about the plane: `de_printat.png`
6274 stars at a median of 3.106 px (7163 at 3.133 before), the 0.17 FITS 5, the 0.1 FITS 8,
Rosetta with stars 650 (649 in the difference image) and without 1, the Veil 1, Andromeda 3116
and 0, Orion 1389 and 0; the full drift is in `docs/validation.json` (V-001, V-002, V-004, V-010,
V-013) and the plan's drift table.

The raw deviation about the border median read a nebula's slope as noise: the gate found the
DarkSky golden scene (a Gaussian nebula of 0.4 over 60 px under 64 stars of 0.3, 0.3 LSB of
noise) down to 50 stars, the slope giving the border a "sigma" near 0.03. The same sigma taken
about a plane fitted to the border by least squares, at 12: the 0.17 FITS 5, the Veil 1, the
Rosetta starless 1; the Rosetta frame with stars 649 stars and 1 grain (grain p90 4.7, max
13.1); `de_printat.png` 6994 of 8395 in the probe (83 % against 65 % with the raw deviation),
the median 3.14.

## Decision

A candidate's amplitude over the border median must reach, beside the absolute floor
`StarMinAmplitude16`, `StarMinSnr` (12) times the border's robust sigma, 1.4826 x the median
absolute deviation of the 96 border pixels about a plane fitted to them by least squares (its
coefficients are the mean and two ratios of sums, the border being symmetric about the centre).
The floor stays: a denoised frame has a sigma near 0 and the floor still refuses its tiny bumps;
a quantised flat border, where more than half the residuals are equal, has a sigma of exactly 0
and the floor alone decides, as before. The owner chose 12 over 10 and 15 on 2026-10-27, on the
table above, and the plane over the raw deviation the same day, on the DarkSky scene.

## Alternatives rejected

- **The deviation about the border median, no plane.** The slope of a nebula under a star
  becomes its "noise": 14 of the DarkSky scene's 64 stars lost, a third of the reference
  image's against a sixth, for no gain on the starless frames (2 against 5 on the 0.17 FITS).
- **10 sigmas.** 9 and 11 left on the two starless reference FITS: under 20, but a frame with a
  little more grain would pass again; the gain is about 10 % of the faint stars.
- **15 sigmas.** Zero everywhere, but 20 to 25 % of the stars of `de_printat.png` lost against
  today, for a median FWHM that moves under 0.1 px either way.
- **The global sigma of the frame.** The stretched noise varies with the level (the stretch's
  gain does): at 10 it leaves 79 on the Rosetta starless frame and 279 on the 0.17 FITS, against
  1 and 9 for the local sigma.
- **A flux or roundness test.** The grain has the stars' scale (about 2 px, correlated) and no
  preferred shape: the 5 x 5 flux leaves 136 on the Rosetta starless frame at 10, and the
  roundness of grain (median 0.73) overlaps the stars' (p10 0.57).
- **Raising the absolute floor.** It would have to climb with the stretch; on a frame stretched
  harder than the reference the grain passes again, and on a gentler one the faint stars go.

## Consequences

- A starless frame now takes R-074: "too few stars for a FWHM", the size table on the pixel-grid
  floor. The star figures of V-001, V-004, V-010 and V-013 move; the drift is in the plan
  `docs/plans/2026-10-27-reject-noise-grain-as-stars.md` and in `docs/validation.md`.
- The FWHM is the bright stars' (12 sigmas and over); faint stars are not counted, and the count
  on a frame with stars drops by a quarter to a third. The median moves by hundredths of a pixel
  on the reference image.
- A frame whose stars all sit on strongly textured nebulosity (a large local sigma) may lose them
  and fall to R-074; the report says so with the count.
- Guards: `R070_NoiseGrainIsNotAStar`, `R070_StarsOverGrainAreStillMeasured`
  (`StarAnalyzerTests`); on the real frames, `StarPairTests` (Category=Validation): every
  starless frame `Insufficient`, every frame with stars kept, and at least 99 % of the stars kept
  on the Rosetta frame with stars in the difference image.
