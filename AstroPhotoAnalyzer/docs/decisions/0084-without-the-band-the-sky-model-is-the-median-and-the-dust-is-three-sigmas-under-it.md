# 0084. Without the band, the sky model is the median of each cell and the dust is three sigmas under it, so a Milky Way frame without a WCS finds the Great Rift

Date: 2026-11-06
Status: accepted 2026-11-06
Supersedes: 0033 in part (the values of `SkyModelLowPercentile` and `SkyNoiseK`)

## Context

ADR 0033 made the classes without the band relative to a sky modelled from all the patches, at
the 25th percentile of each cell "so a nebula covering half a cell does not lift it", with
dust and nebula at 4 robust sigmas under and over the model. It was measured on deep-sky
frames (Rosetta, Orion, Andromeda) and on the two reference images, and
`docs/design/known-limitations.md` recorded that on the sky path the Great Rift is not found:
the 25th percentile sinks into the Rift, which is then never under the sky, and the dust
class stays under the `DustClassMinFraction` floor, so the darkest 1 % of the sky decides.

On 2026-11-06 the owner asked, on the Smida Milky Way frame of 2026-08-12 (the folder
`...\Processed_1mmPTFE_Flats\Crop\RBF_best_effort\deepPrismV2_max_0.75\StarNet\stretch0.12-logd4\recomp\`,
a plate-solved FITS and a TIFF of the same frame with the stars put back, 5837 x 3867,
background 0.126, identical patch L\*s), that without a WCS the Rift and the Pipe count as
dust, with the dust and sky figures close to the WCS path's, and whether thresholds alone could
do it.

### Measured on 2026-11-06

With a scratchpad probe over the core, on the 32 px patches (21 840), baryta at Dmax 2.4:

| | with the WCS (band path) | without, as it was | without, median and 3 sigma |
| --- | --- | --- | --- |
| dust class, % of the patches | 9.5 | 0.9 | 9.8 |
| the verdict's dust, L\* | 4.85 (the class's 25 %) | 3.48 (the darkest 1 % of the sky) | 4.90 (the class) |
| the sky, L\* | 12.0 | 11.9 | 12.1 |
| nebula / bright nebula, % | 22.4 (band) / 1.7 | 17.9 / 3.2 | 11.8 / 2.5 |

The sweep over the sky path's constants (the cell percentile 25 or 50, the cell 20, 30 or 40
patches, the factor 4 to 1, and a fixed depth in L\* in place of the factor):

- With the 25th percentile no factor reaches the band path's figure without filling the class
  with sky: at 1.5 sigma the class is 5.6 % at L\* 3.9 with 72 % of it the band path's dust; at
  1 sigma 8.0 % at 4.5 with 59 %.
- With the median, 3 sigma gives 9.8 % at 4.90, finding 73 % of the band path's dust patches
  (1503 of 2062), the class 70 % the band path's dust (the rest 281 sky and 365 band patches at
  the Rift's edges); 4 sigma gives 7.2 % at 4.20 (62 %, 82 %); 2.5 sigma 11.7 % at 5.34. A cell
  of 40 patches with the median and 3 sigma gives 8.1 % at 4.46 (72 %, 84 %). A fixed depth
  under the model is a stretch-dependent constant and was not pursued.
- The reference FITS `starless_rbf0.5_stretch0.17.fit` run without its WCS: 11.0 % at 8.20,
  sky 17.7, against the band path's 9.4 % at 7.90 and 17.6 (today 6.91 from the darkest sky).
  The same frame at a background of 0.10 (`starless_rbf0.5_stretch0.1.fit`): 10.8 % at 3.85
  against the band path's 5.7 % at 3.20: the class keeps its share of the frame across the
  stretch, while the band path's `DustDepthLstar` (5 L\*) is an absolute depth and finds less
  of the Rift on the darker stretch.
- The sigma is the robust sigma of the patch residuals, and a patch is the mean of 1024
  pixels: adding 0, 1, 2, 5 and 10 levels of gaussian pixel noise to the denoised Smida frame
  moves it from 1.17 to 1.17, 1.18, 1.22 and 1.33 L\*, and the class from 9.8 to 8.5 %. The
  threshold follows the stretch and the stars, not the denoising.
- The same frame starless (the FITS one folder up): the band path 9.1 % at 4.03, the sky
  11.3; the median and 3 sigma 13.3 % at 4.92 (the sigma 0.81 against 1.17 with the stars, so
  the threshold is lower and the class larger); 4 sigma 10.4 % at 4.29. The stars lift the
  dust patches on both paths (4.03 to 4.85 with the WCS). The print file is the one with the
  stars.
- The deep-sky frames ADR 0033 was measured on, with the median and 3 sigma: the verdict's dust
  does not move on any (Rosetta 6.15, Orion 10.9, Andromeda 1.50, the Veil 6.3 to 6.4: the
  class stays under the 5 % floor or lighter than the darkest sky); the nebula class shrinks
  (Rosetta starless 12.6 to 8.8 %, Orion 18.4 to 11.8 %, Andromeda 23.8 to 16.0 %) and the
  curve anchors rise (Rosetta 29.5 / 45.2 to 34.8 / 47.8, Orion 39.6 / 76.3 to 47.4 / 77.9,
  Andromeda 18.1 / 38.9 to 21.4 / 44.1; Rosetta with its stars 34.2 against 34.8 starless, the
  speckles still under 1 L\*). `de_printat.png` moves from 4.18 / 8.87 to
  4.40 / 9.03 (the class is 7.8 % but its 25 % percentile, 6.10, is lighter than the darkest
  sky, so the sky still decides).

## Decision

The owner chose, on 2026-11-06, both thresholds: `SkyModelLowPercentile` 25 -> 50 (the model is
the median of each cell) and `SkyNoiseK` 4 -> 3. Both stay relative: a rank inside the cell and
a multiple of the sky's own noise measured on the image, so a stretch to any background level
and any amount of denoising leave the rule the same. Nothing else of ADR 0033 changes: the
clipping pass, the Otsu split, the 5 % floor, the one measurement on both paths.

## Alternatives rejected

- **The 25th percentile with a lower factor.** Fills the class with sky before it reaches the
  Rift (see above).
- **The median with 4 sigma.** One threshold instead of two, but 4.20 against 4.85 on the
  frame asked for, finding 62 % of the band path's dust; closer on the starless frame (4.29
  against 4.03), which is not the print file.
- **A separate factor for the dust side, the nebula side kept at 4.** A new constant; the
  nebula class with the median model is already smaller at 4 (9.5 %) than at 3 (11.8 %), so
  it does not keep ADR 0033's classes either.
- **A fixed depth in L\* under the model, or a depth as a share of the sky's level.** A
  constant chosen by hand, stretch-dependent or not, in place of one read from the image.
- **The band path's rule without geometry (a depth under the local median of a window).**
  Finds 34 to 74 % of the band path's dust depending on the window and the depth, both
  constants chosen by hand.

## Consequences

- `Thresholds.cs`: the two values and their comments. R-044 rewritten with the values and the
  re-measured examples. `docs/design/thresholds.md`, `known-limitations.md` (the Rift bullet),
  the README's sky-path paragraph.
- The figures of every sky-path case move: V-001, V-003, V-005, V-009 (`de_printat.png`),
  V-010, V-011 (Rosetta), V-012 (Orion), V-013, V-014 (the Veil); the band-path cases V-004
  and V-008 do not. The golden files of the synthetic sky-path scenes change where their classes
  do.
- The synthetic `dark-sky` golden scene (512 px, one model cell, a halo over most of it) now
  classes 30 % of its frame as dust and reports the dust 2 L\* over its blob: the median of the
  one cell is lifted by the halo, and the plain sky's lower half falls under it by more than
  the sky's noise (`known-limitations.md`). No real frame is one cell wide; no test guards the
  scene's dust share.
- The band path keeps its absolute `DustDepthLstar`, which the 0.10 stretch showed to find
  less of the Rift on a darker stretch: a task of its own, if wanted.
