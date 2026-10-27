# The edge regions are measured on their own sky pixels

Branch `claude/Sky-colour-from-the-sky-patches`, the same branch and worktree as ADR 0055, on
the owner's word ("on the same branch, after ADR 0055"). The approved plan is saved to
`docs/plans/2026-10-27-edge-regions-on-sky-patches.md`.

## Context

ADR 0055 moved the reference of section 3 to the sky patches, but each edge region still took
its medians on every pixel it holds, so the Milky Way band entering a corner, bluer than the
sky, read as a coloured departure (the reference image's top-left corner, +6.5 / +5.0 / +8.8),
and ADR 0055 left "measure each region on its own sky patches" unmeasured, with the risk that a
horizon glow bright enough to be classed nebula would leave the region with the object.

Measured on 2026-10-27 with a probe test (not committed) on the five real images: each region's
deltas against the ADR 0055 reference, on every pixel (as before) and on its sky pixels alone,
with the share of sky in the region (`!C` coloured, `!N` neutral):

| Image | Region | Every pixel | Sky pixels | Sky |
|---|---|---|---|---|
| `de_printat.png` | top-left (the band enters) | +6.5 / +5.1 / +8.7 `!C` | +4.2 / +2.9 / +5.6 `!N` | 77 % |
| | bottom-right (horizon) | +4.5 / -3.6 / -17.0 `!C` | +4.1 / -4.0 / -17.1 `!C` | 84 % |
| | top (the band crosses) | +6.8 / +5.2 / +6.4 `!N` | +3.9 / +3.2 / +3.8 `!N` | 72 % |
| | bottom (horizon) | +8.4 / -0.7 / -7.8 `!C` | +3.0 / -4.0 / -11.3 `!C` | 60 % |
| the 2026-08-12 core | top-left (the Rift's foot) | -3.4 / -2.9 / -6.5 `!C` | -5.4 / -4.5 / -8.7 `!C` | 68 % |
| | bottom-right (horizon) | +1.8 / -3.4 / -6.7 `!C` | +2.7 / -2.5 / -5.3 `!C` | 87 % |
| | bottom (horizon) | +6.3 / -1.8 / -8.6 `!C` | +0.3 / -2.6 / -5.2 `!C` | 41 % |
| `starless_rbf0.5_stretch0.17.fit` | top (the band crosses) | +0.8 / -0.9 / -4.0 `!C` | -1.3 / -1.7 / -4.2 `!N` | 53 % |
| | bottom (horizon) | +12.3 / -0.9 / -12.1 `!C` | +1.4 / -4.0 / -5.0 `!C` | 41 % |
| `Rosetta_starless_no_WCS.fit`, `Veil_Starless.fit` | all eight | unchanged (92 to 100 % sky) | | |

Of forty readings two change their verdict, both the band read as coloured, and both become
neutral. The two horizon regions keep their coloured flag on both cores; the glow's red excess
shrinks on the plate-solved FITS (+12.3 to +1.4 on the bottom edge) because its brightest part
is classed band or bright cloud, and its blue deficit keeps the flag. The lowest sky share of
any region is 41 %. The top-left corner of the 2026-08-12 core reads darker and browner on its
own sky pixels than on every pixel: its sky is darker and less blue than the frame's, the
Rift's diffuse foot or a residual, which the eye must judge.

## Decision

Each edge region takes its per-plane medians on its sky pixels, the patches R-044 calls sky,
when they are at least `RegionSkyMinFraction` (25 %) of the region; under that, on every pixel,
and the row says so (`(sky 8 %: every pixel)`). The regions move to the map's subsample (stride
4), where the classes can be read; `BackgroundStride` (3) goes. ADR 0056 records it and
supersedes the regions' part of ADR 0055.

## Steps

1. R-061 restated; tests in `BackgroundAnalyzerTests` for the sky pixels and the floor.
2. `BackgroundAnalyzer.Regions(Raster, DustMap, double[])`; `EdgeRegion.SkyFraction`,
   `OnSkyPixels`, `PixelsNote`; the report's Appendix E row, the coloured recommendation's
   `Because`, the window's region rows and the Culoare guide.
3. Goldens, `/validate` (the regions' levels move to stride 4, and their flags where the object
   leaves), the drift recorded.
4. ADR 0056; ADR 0055's last alternative and ADR 0018's dated note restated (both unmerged on
   this branch); `measurements.md`, `thresholds.md`, `known-limitations.md`, `window.md`,
   `validation.md`, CHANGELOG.

## Deviations

Noted at the wrap-up of 2026-10-27.

- The test of the quarter floor needed its lane to end on a patch boundary: on a flat synthetic
  sky the sky's sigma is 0, so a patch half covered by the lane is not sky, and a lane to x 240
  left 8 % of the region as sky instead of the share its width suggested. The test uses a lane to
  x 224, a sixth of the region as sky.
- Looking at the window afterwards, the owner found the top-left corner of the 2026-08-12 core
  named a horizon glow while it is darker than the sky; the naming of a darker region followed,
  outside this plan, and was recorded in ADR 0056's consequences.
