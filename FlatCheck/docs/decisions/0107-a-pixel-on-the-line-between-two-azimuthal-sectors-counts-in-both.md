# 0107. A pixel on the line between two azimuthal sectors counts in both

Date: 2026-11-13
Status: accepted 2026-11-13

## Context

ADR 0095 made the structure figures the same for a frame and its mirror (R-131). The azimuthal
asymmetry cuts each ring into twelve sectors of 30 degrees around the frame's geometric centre
and compares the sector medians. `Metrics.Azimuth` puts a pixel whose `dx` or `dy` is exactly 0
on the positive side. On a raster of even width and height no pixel has such a coordinate (the
centre lies between pixels). On an odd width the centre column has `dx = 0`, on an odd height
the centre row has `dy = 0`: a line of pixels exactly on a sector boundary, which a mirror
leaves in place, so that in the mirrored frame the same pixels fall in the other sector. The
other ten boundaries, at the multiples of 30 degrees that are not multiples of 90, pass through
no pixel centre (their slopes are irrational).

The review of 2026-11-13 (CR-010) found it by reading and measured it on a Python replica.
Measured in C# the same day:

| Raster | As read | Rows mirrored | Columns mirrored |
|---|---:|---:|---:|
| 459 x 305, through `StructureAnalyzer` (frame 918 x 610) | 4.216432 | 4.218383 | 4.215500 |
| 526 x 259, through `StructureAnalyzer` (frame 1052 x 518) | 3.361315 | 3.371617 | 3.361315 |
| 1376 x 917, `Metrics.AzimuthalAsymmetry` (the raster of an 8256 x 5504 frame) | 4.239522 | 4.237840 | 4.239522 |
| 263 x 259, `Metrics.AzimuthalAsymmetry` | 5.404240 | 5.404240 | 5.403712 |
| 264 x 260, even | 5.417889 | 5.417889 | 5.417889 |

R-131 named the 8256 x 5504 frame as covered; it was not. The two rules that make the figure
the same every way up, on the 1376 x 917 raster: the boundary pixel in both sectors 4.236745,
in neither 4.240617; on 263 x 259 the first gives 5.403712.

## Decision

The owner's choice of 2026-11-13, between "in both sectors" and "in neither":

1. In `Metrics.AzimuthalAsymmetry`, a pixel with `dx == 0` or `dy == 0` is added whole to the
   two sectors that meet on that half-axis (`Metrics.AxisSectors`).
2. `Metrics.Azimuth` is not changed. Its other caller, the ring fit of `RadialModel`, works on
   the raster symmetrised at 180 degrees, where the two sides of an axis hold the same values.

It is the rule ADR 0095 already has for a statistic that is a percentile: the 15th-percentile
background tiles of `--light` take a boundary row whole in both neighbours, since a weight of
one half has no meaning for a median.

## Alternatives rejected

- **In neither sector.** As symmetric, and no pixel is used twice, but it throws away a row and
  a column of the raster, and it would be a second rule beside the one of ADR 0095 for the same
  situation.
- **Half a weight in each.** A weighted median for one line of pixels; the sector medians are
  plain medians everywhere else.
- **Moving the centre off the pixel grid.** The geometric centre is R-014's and the other
  figures are built on it.

## Consequences

- On an odd raster the figure moves by 0.0003 to 0.003 points from what it was (the three
  rasters measured), and is the same for the frame and its mirrors, which differed by up to
  0.01. On an even raster nothing changes: no pixel has a zero
  coordinate.
- The D3300's frames (6016 x 4016, raster 1504 x 1004) are even, so no figure of the validation
  library moves, and none of the 39 parity goldens changes (their fixtures are even too). A
  40th scenario, `structure odd raster 526x518`, was added to reach the new branch; it prints
  two decimals, so it shows that the branch runs and does not pin the rule.
- The two sectors beside an axis share one line of pixels each, so their medians are a little
  closer than with disjoint sets; on the 1376 x 917 raster the figure is 0.003 points below the
  one-sided reading.
- R-131 holds the behaviour; `R131_APixelOnTheLineBetweenTwoSectorsCountsInBoth_NotInNeither`
  pins the rule's figure, and `R131_TheAzimuthalAsymmetryIsTheSame_WhenAnOddRasterIsMirrored`,
  `R131_AzimuthalAsymmetry_IsTheSameForARasterAndItsMirrors` and
  `R131_AxisSectors_AreTheSectorsOnEitherSideOfTheAxis` pin it.
