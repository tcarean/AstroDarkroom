# 0095. The geometry is symmetric about the frame's centre, so a figure does not depend on the way up a frame is read

Date: 2026-11-09
Status: accepted 2026-11-09
Supersedes: 0013 in part (the index centre); 0016 in part (the top-left tile grid)

## Context

ADR 0094 made a FITS read with the file's first row last, as Siril shows it, and the figures
moved: 17 of the 36 validation cases drifted by 0.05 to 0.58 points when the same frame was read
the other way up. The analysis was anchored on pixel indices in three places. The radial model,
the azimuthal asymmetry and the tilt fit were built around the index `(w/2, h/2)` (ADR 0013),
half a pixel below the geometric centre `((w-1)/2, (h-1)/2)` of pixel indices `0..w-1`.
`Metrics.FitCenter` sampled every `step`-th row and column from index 0. And the tile grids of
`LargeScalePtp`, `MaxTileDeviation`, `LocalSpots` and `TileBackground` started at row 0 and left
the leftover rows and columns at the far edge (ADR 0016). None of the three is its own mirror.

Measured on 2026-11-09 on the 15 structure masters of `docs/validation.json`, each read the
right way up and upside down, with temporary switches for each change (the harness and the
figures are in the task's scratchpad; the table below is the summary), the largest difference
between the two orientations over the 15 masters, in points:

| geometry | corners | azimuthal | residual | tilt | worst tile | local spot |
| --- | --- | --- | --- | --- | --- | --- |
| today (ADR 0013, 0016) | 0 | 0.195 | 0.162 | 0.287 | 0.336 | 0.577 |
| geometric centre | 0 | 0 | 0.084 | 0.044 | 0.419 | 0.799 |
| centre + every pixel in the tilt fit | 0 | 0 | 0.084 | 0 | 0.419 | 0.799 |
| centre + tilt fit + centred integer tile grid | 0 | 0 | 0.111 | 0 | 0.435 | 0.001 |

What the last row left was the odd leftover of the tile grid (1004 rows = 27 tiles of 37 plus
5, which no integer offset splits evenly), visible on V-009 alone, a master with a sharp edge in
its residual. The owner chose, from four options, the exact one: half-weight boundary rows when
the leftover is odd, and the parity goldens and the moved validation cases re-recorded.

Making it exact uncovered two more asymmetries, both discrete decisions tipped by the last bit:

- The block mean of `Img.Prepare` summed a 2x2 block in raster order, so the mirror of a
  raster differed from the mirrored raster by one ulp; the ring fit's inlier cut (2.5 MAD) tipped
  on that bit for one sector and moved the model by 1e-4 in the corners. Likewise the sums over
  a sector's pixels and over the sectors inside `RingFit`.
- `Math.Atan2` and `Math.Cos` on the full circle differ in their last bits between a pixel and
  its mirror, and on the diagonals, which are sector boundaries at 24 sectors of 15 degrees, a
  pixel fell in one sector one way up and in the neighbour the other way up.

And one old belief fell: the "profile jump on partial rings under a gradient" that motivated
`Img.Symmetrise` (ADR 0017: 1.54 % on a smooth fixture) was mostly the index centre's half
pixel, which left the two arcs of a ring with unequal pixel counts so the median fell on one mode
or the other. With the arcs balanced a pure gradient's raw profile reads 0.093 % against 0.011 %
symmetrised; the symmetrisation stays, for the fragility of a median that sits in a gap (0.32 %
against 0.23 % with 2 % of noise on top).

## Decision

1. **The geometric centre.** `Metrics.Centre(n) = (n - 1) / 2`; `StructureAnalyzer`,
   `LightAnalyzer` and `FitCenter` build their geometry around it (R-014 amended).
2. **Every pixel in the tilt fit.** `FitCenter` reads the whole raster; the sampling step is
   gone. Not measurable in time on a 1504 x 1004 raster.
3. **Tile grids symmetric about the frame's centre.** `Img.BlockMeanCentred`: `H / k` tiles down
   and `W / k` across, the leftover split between the two edges, and when it is odd the boundary
   row or column counts half in each edge tile (a pixel's weight is its overlap with the tile).
   `LargeScalePtp`, `MaxTileDeviation` and `LocalSpots` use it; `LocalSpots` reports the tile's
   centre in pixel-index coordinates. `TileBackground`, whose tiles take a percentile, lays the
   same grid and gives the boundary row whole to both tiles it straddles.
4. **Order-independent sums where a threshold follows.** `Img.BlockMean` sums each block's values
   in sorted order; `RingFit` sums in `decimal`, exact at this scale.
5. **Mirror-exact azimuths.** `Metrics.Azimuth` computes the sector, `cos 2t` and `sin 2t` in the
   first quadrant from `|dx|`, `|dy|` and reflects them by the signs; `RadialModel` and
   `AzimuthalAsymmetry` use it.
6. **The fixtures follow.** `FitsTestData.Radial`, `WithFourfold`, `WithEvenHarmonics` and
   `TestImages.InvertedRadial` are centred on the geometric centre, like the analysis.
7. **`Img.Prepare` stays anchored at (0, 0)**: its 2x2 block mean follows the Bayer phase. On an
   odd-sized frame one dropped row or column still sits at the far edge; every sensor the tool
   has met is even-sized, and R-131 states the guarantee for even sizes.

Measured with the final code on the 15 masters: every figure equal to the last bit both ways up,
no verdict differs (R-131).

## Alternatives rejected

- **The centred integer grid with the extra row at the bottom** (variant A): simpler, but it
  left the residual and the worst tile with a one-row asymmetry, 0.11 and 0.44 points on V-009.
- **The centre and the tilt fit alone** (variant C): exact azimuthal asymmetry and tilt, but the
  worst tile and the local spot stayed at 0.4 to 0.8 points.
- **Keeping the dependence** (variant D), documented in ADR 0094: at most 0.6 points, below the
  verdict bands, but a figure that is a property of the orientation and not of the frame.
- **Tolerating the last-bit asymmetries** instead of the sorted and decimal sums: the differences
  were 1e-4 points, below the printed hundredth, but a robust fit with a hard threshold turns an
  ulp into a visible move whenever a sector sits on the edge, and the requirement says exact.

## Consequences

- The figures of the real masters moved against ADR 0094's: 18 of 36 validation cases
  re-recorded (35 figures, 0.05 to 0.56 points; `docs/validation.md`, History). The verdict of
  V-012 moves from PERFECT to BUN on its tilt, 1.930 to 2.001 %, right on the 2 % edge, and
  V-027 and V-039, the same master, carry that shape verdict. No depth moved.
- The parity goldens of the structure and light scenarios changed their figures, with the
  owner's yes, and `python-parity.md` records that their figures are no longer the prototype's:
  the prototype's geometry was the index one. The synthetic fixtures, centred like the model
  now, read what a perfect flat should: the clean fixture's corner spread 0.57 to 0.00 %, residual
  0.28 to 0.05 %, illumination offset 0.19 to 0.00 %; the fixture with profiles reads its shape
  PERFECT where the half pixel made it BUN.
- Known issue 5 (the corner spread's floor) is closed; the tests that pinned the half-pixel
  artefact now pin its absence (`CornerStats_OnACentredRadialField_SeesNoSpreadAtAnySize`,
  `Symmetrise_OnTheFixtureFieldChangesNothing`).
- Guards: `R131_*` in `MetricsTests` (the centred block mean's weights, its mirror, the tilt fit
  on a mirrored raster), `StructureAnalyzerTests` (an asymmetric scene read three ways, two
  sizes, one with odd tile leftovers) and `LightAnalyzerTests` (the same for the light figures).
