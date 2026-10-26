# The smoothed-flat mode: what was measured before and while building it

> Moved here from `PLAN-SMOOTH.md` when that plan was deleted after implementation (2026-09-16),
> then cut on 2026-09-27 to what neither CLAUDE.md nor a decision record says (ADR 0051): ADR
> 0026 carries the design, the rejected alternatives and the acceptance figures in summary, ADR
> 0027 the scale rule and its limits, `modes-smooth.md` the mechanics; this note keeps the
> measurements behind them, to the second decimal, on the five masters named below.
> **Covers:** `FlatCheck.Core/Analysis/SmoothAnalyzer.cs` (and `Img.TileMedians`,
> `Mosaic.Merge`) and, under "Choosing the scale", `SmoothScaleAdvisor.cs` and
> `Metrics.FineTexture`, as of 2026-09-16. **If that code changes, re-run the acceptance figures
> below on the five masters and update this file**; the tables are the evidence that the method
> does what ADR 0026 says it does, and a table that no longer matches the code must not be left
> standing. The quickest re-run is `flatcheck <master> --smooth out.fit` on each of the five,
> compared with the "removed component" table to the second decimal; the same-lens pair ratio,
> the fine-component measurement and the synthetic with known truth need a few dozen lines of
> numpy each.

Two details differ from the plan as it was written: the scale is the kernel's **sigma** in
sensor pixels (300 px), not a "diameter", and the window shows the two structure batteries
as two stacked cards rather than two columns.

## Part 0 - What was measured before writing this (2026-09-16), and what it decided

Five masters from the flats library, all Siril float, 6016 x 4016, RGGB, `STACKCNT` 30:

| name | lens / source | corner spread | worst tile | local spot |
|---|---|---|---|---|
| nikkor_iso400 | Nikkor 35 DX, sky, ISO 400 | 4.00 % | 2.90 % | -0.44 % |
| nikkor_iso800 | Nikkor 35 DX, sky, ISO 800 | 10.96 % | 5.52 % | +0.36 % |
| tokina_aproape | Tokina 20, monitor, diffuser at the lens | 6.67 % | 6.61 % | -0.50 % |
| tokina_pemonitor | Tokina 20, monitor, diffuser on the monitor | 16.41 % | 9.75 % | +6.19 % |
| samyang_lcd | Samyang 135, LCD (non-uniform source) | 3.49 % | 1.86 % | +0.22 % |

### The texture, measured on one Bayer position (G1)

Fine component = `G1 / gaussian(G1, sigma)`, 200 px in from the border, at position
resolution (half the sensor's). Pixel noise 0.49-0.53 %.

| master | structured fine part, sigma 16 / 32 / 64 | p1 / p99 at sigma 64 | deepest pixel |
|---|---|---|---|
| tokina_aproape | 0.06 / 0.07 / 0.13 % | -1.10 / +1.26 % | -13.9 % (dust) |
| tokina_pemonitor | 0.32 / 0.72 / 1.19 % | -2.84 / +3.25 % | -27.0 % and +35.5 % |

**The texture is not a weave, it is blotches**: soft cells ~300 sensor px across, no
periodic direction, spectral peaks at 290-340 px and a weaker family at 90-100 px. The
defocused fabric at monitor distance. With the diffuser at the lens it is under 0.1 %.

### Three designs were tried; two failed on the real masters

ADR 0026 holds the three designs, why the first two were rejected and their headline figures on
the five masters: what the global polynomial left and how it oscillated, the ring amplitude the
radial model put into the file, the clip floor's effect on the Samyang and tokina_pemonitor
tiles, and the edge band the partial tiles caused. What it does not record:

1. **Global polynomial over the residual of a ring-median radial model** (degree 2-10). The
   mid-scale structure it could not follow was different on every master: the monitor's own
   blotches at ~1000-1500 px and ±3 % on tokina_aproape, a decentred ring on the Nikkor, LCD
   blobs at ±0.7 % on the Samyang. The tile RMS after the fit kept falling up to degree 10 with
   no floor. "Large-scale structure is low-order" was wrong, and a polynomial basis has no
   scale knob.
2. **Ring-median radial model x local-linear field (LOESS-1) on the tile grid.** The mechanism
   of the rings: beyond the inscribed circle a ring is partial arcs, any even non-radial
   structure makes the ring distribution bimodal and the median jumps bin to bin, the artefact
   ADR 0017 and `radial-model-even-term.md` record. The figures were measured with the plain
   ring median; since 2026-09-17 `Metrics.RadialModel` also takes the even 2θ term off the
   partial rings, but the rejection does not rest on the artefact alone. The rings showed as
   concentric circles in the ratio of two smoothed masters, and smoothing the 1-D profile did
   not remove them: the jumps are below the profile smoother's scale and above the field's. A
   radial model of any kind is the wrong tool here: **the split between "radial" and "the
   rest" is arbitrary, and the only requirement is that the output be smooth and follow the
   data.**
3. **No radial model. Tile medians, local quadratic (LOESS-2), robust, evaluated per pixel.**
   This is the design. It has no rings by construction, since nothing in it knows about a
   centre; it follows the vignetting's curvature to the corner, because a quadratic over a
   ±450 px window of a profile whose scale is ~1800 px is exact to third order; its one knob is
   a length scale. Two details cost a round each and are recorded so they are not rediscovered:
   - **The clipping needs an absolute floor.** Relative to the MAD alone, anything the local
     quadratic did not follow at 0.15 % became an "outlier" and was then not followed at all.
     With the floor, the tiles rejected on tokina_pemonitor are the two dust spots, the bright
     spot and the deepest cells, which are exactly the things that should go.
   - **Partial tiles must sit where they are.** The grid keeps the partial last row and column
     (58 rows and 8 columns on this sensor) so nothing is extrapolated. The first cut fitted
     them one full tile from their neighbour; besides the band along the right edge, the
     synthetic showed a 0.2 % band along the bottom.

### The chosen pipeline, per Bayer position (mono: the whole frame)

1. Tile grid: `k = Metrics.TileSize` (75 px at position resolution = 150 sensor px),
   `ceil(h/k) x ceil(w/k)` tiles, **median** per tile, real tile centres (partial last
   row/column included).
2. Local quadratic in `(dx, dy)` (tile units, real centre offsets) at every tile, Gaussian
   kernel `sigma_t` tiles, terms `1, x, y, x², xy, y²`, solved as 6x6 normal equations.
   `sigma_t = 2` by default = 150 position px = **300 sensor px**.
3. Robust: 3 rounds of two-sided clipping at `max(2.5 · MAD, 0.02)` of the residual
   relative to the tile level, rejected tiles get weight 0 in every neighbour's fit.
4. Per pixel: bilinear blend of the four surrounding tiles' quadratic predictions,
   each evaluated at the pixel's real offset from that tile's centre. Extrapolates
   quadratically for the half tile beyond the outermost centres.
5. `Mosaic.Merge` of the four positions. Nothing is renormalised.

### Results on the synthetic with known truth (one position, 3008 x 2008)

Truth = vignetting to 55 % at the corner x (6 % gradient + 3 % + 2 % saddle) x, in
B and C, mid-scale blobs (±3 %, sigma 1200 sensor px); contamination = 0.5 % noise,
three -15 % dust spots, and in C the real-like cells (400 Gaussians, sigma 75 sensor
px, ±3 %). Error = `smoothed / truth - 1`, sigma_t = 2:

| case | interior p99 | interior max | border band max | rms |
|---|---|---|---|---|
| vignetting + gradient + saddle + noise | 0.11 % | 0.12 % | 0.19 % | 0.04 % |
| + mid-scale blobs + dust | 0.27 % | 0.59 % | 1.08 % | 0.07 % |
| + cells (the pe-monitor case) | 1.06 % | 1.44 % | 1.89 % | 0.35 % |

The first row is the method's floor. The second row's border figure is a ±3 % blob
centred near the edge, where the one-sided quadratic extrapolates its cubic term - real
masters show no such band (below). The third row is **what the cells leave behind**: a
random field with a 300 px correlation length passes a 300 px kernel at ~30 % in rms.
That residual is fundamental to any low-pass with a finite kernel and is the price of
keeping mid-scale structure; a wider kernel trades it against the blobs (sigma 3: rms
0.33 %, but the clean case's floor rises to 0.17 % rms and 0.38 % max).

### Results on the five real masters, sigma_t = 2

The structure battery of the tool itself (`flatcheck.exe` 2.1.0) on the original and on
the smoothed file:

| master | corners | azimuthal | residual | tilt | worst tile | local spot |
|---|---|---|---|---|---|---|
| nikkor_iso400 | 4.00 → 3.94 | 3.75 → 3.68 | 3.90 → 3.80 | 1.75 → 1.72 | 2.90 → 2.74 | -0.44 → +0.19 |
| nikkor_iso800 | 10.96 → 10.74 | 5.26 → 5.05 | 7.13 → 7.13 | 7.97 → 7.95 | 5.52 → 5.54 | +0.36 → +0.20 |
| tokina_aproape | 6.67 → 6.51 | 8.73 → 8.53 | 6.51 → 6.33 | 6.71 → 6.69 | 6.61 → 6.46 | -0.50 → -0.34 |
| tokina_pemonitor | 16.41 → 15.92 | 10.74 → 10.74 | 11.97 → 11.19 | 11.77 → 11.76 | 9.75 → 8.28 | **+6.19 → +0.62** |
| samyang_lcd | 3.49 → 3.56 | 2.67 → 2.65 | 3.00 → 3.02 | 4.04 → 4.05 | 1.86 → 1.87 | +0.22 → +0.09 |

Every verdict and every vignetting depth (0.63 / 0.62 / 0.85 / 0.77 / 0.63 stops) is
unchanged. The removed component, `Prepare(input) / Prepare(smoothed)`:

| master | p1 / p99 | ptp (p95-p5) | worst tile | border band p99 (tile-averaged) |
|---|---|---|---|---|
| nikkor_iso400 | -0.49 / +0.51 % | 0.38 % | 0.40 % | 0.29 % |
| nikkor_iso800 | -0.57 / +0.61 % | 0.39 % | 0.45 % | 0.25 % |
| tokina_aproape | -0.46 / +0.48 % | 0.38 % | 0.47 % | 0.28 % |
| tokina_pemonitor | -3.46 / +3.93 % | 3.68 % | 4.21 % | 3.42 % |
| samyang_lcd | -0.56 / +0.60 % | 0.12 % | 0.71 % | 0.17 % |

- On the four clean masters the removed component is pixel noise (p1/p99 ≈ ±0.5 % is
  the 0.5 % per-pixel noise at analysis resolution) plus the dust donuts, plus 0.3-0.5 %
  of the Nikkor's decentred ring and the Samyang's LCD centre hot spot - features ~300 px
  wide that sit at the kernel's scale and are partly taken. `sigma_t = 1.5` halves that
  (0.29 % ptp on the Nikkor) at the cost of more cell residual; the scale is the one
  knob and it is exposed.
- On tokina_pemonitor the cells and the two spots are what comes out. What remains of
  the texture in the file: fine structured part 1.19 % → 0.11 %, p1/p99 at sigma 64
  from -2.84/+3.25 % to -0.15/+0.34 %.
- No ring artefact: ±0.02 % on the Nikkor's removed component, and the ratio of the two
  smoothed Nikkor masters has ±0.02 %.
- CFA intact: the four position levels move by < 0.1 %, the G1/G2 ratio is preserved to
  the fourth decimal, no chequerboard; the smoothed G1's largest second difference is
  0.005 % (the file is smooth to the pixel).
- The border band of the removed component is *lower* than the interior on every clean
  master: no edge artefact on real data.
- Same-lens pairs: Nikkor 400/800 ratio 8.54 % → 8.53 % ptp (the two sky flats differ by
  a sky gradient and the smoothing does not touch it); Tokina pair 15.13 % → 13.58 %,
  and the compact spot in that ratio goes from -6.60 % to -0.43 %.
- Cost in numpy: ~10 s per 24 MP master for the four positions, of which the tile
  medians and the per-pixel evaluation dominate. Memory: one position raster (48 MB) and
  its temporaries at a time, plus the input and output at full resolution.

### What the mode keeps that may not be the lens, stated in the report

ADR 0026 records the limit: everything smooth stays, the lens's or the source's (the monitor's
blotches, a sky flat's gradient, a non-uniform source's corner inequality), because the pixels
do not say which it is, and nothing in the tool separates the two since the `--ratio` mode
left (ADR 0067). The mode removes texture, dust
and blotches; it does not remove illumination.


## Choosing the scale (measured 2026-09-16, behind `--scale auto`)

**Covers:** `FlatCheck.Core/Analysis/SmoothScaleAdvisor.cs`, `Metrics.FineTexture` and the
three `Th.SmoothTexture*` / `Th.SmoothScaleLoss` thresholds. Re-run on the five masters if
any of them changes: `flatcheck <master> --smooth out.fit --scale auto` prints everything
in the tables below.

Three scales on the five masters, through `flatcheck --smooth`. Per scale: the texture left
in the smoothed file (structured part of the fine component under ~128 sensor px, pixel
noise subtracted), the change in the azimuthal asymmetry and the tilt (after minus before,
points), and the removed component block-meaned over 2-sigma blocks (max deviation, %).
The first table is the numpy measurement of the sweep (Gaussian of sigma 64 on the G1
position raster); the second is the C# metric that shipped (double box of W/47 on the
analysis raster).

| master | texture in (numpy) | 150: texture, azim, tilt, L2 | 300 | 600 |
|---|---|---|---|---|
| nikkor_iso400 | 0.13 % | 0.09, 0.00, -0.01, 0.11 | 0.08, -0.07, -0.03, 0.14 | 0.06, **-0.79**, -0.14, 0.24 |
| nikkor_iso800 | 0.14 % | 0.09, -0.08, -0.01, 0.12 | 0.08, -0.21, -0.02, 0.22 | 0.06, **-0.49**, -0.07, 0.25 |
| tokina_aproape | 0.13 % | 0.11, -0.08, 0.00, 0.10 | 0.10, -0.20, -0.02, 0.24 | 0.08, **-0.80**, -0.14, **1.71** |
| tokina_pemonitor | **1.19 %** | **0.33**, -0.20, -0.11, 1.22 | 0.11, 0.00, -0.01, 0.82 | 0.07, -0.19, +0.11, 0.89 |
| samyang_lcd | 0.07 % | 0.05, -0.01, -0.01, 0.12 | 0.05, -0.02, +0.01, 0.17 | 0.03, -0.08, +0.02, 0.60 |

| master | texture in (C#) | after 150 | after 300 | after 600 |
|---|---|---|---|---|
| nikkor_iso400 | 0.05 % | 0.02 | 0.01 | 0.01 |
| nikkor_iso800 | 0.06 % | 0.02 | 0.01 | 0.01 |
| tokina_aproape | 0.04 % | 0.02 | 0.02 | 0.01 |
| tokina_pemonitor | **0.69 %** | **0.09** | 0.02 | 0.01 |
| samyang_lcd | 0.04 % | 0.01 | 0.01 | 0.01 |

What it decided, the three thresholds (`Th.SmoothTextureMin`, `Th.SmoothTextureGone`,
`Th.SmoothScaleLoss`), the guards tried and rejected (corners, residual and worst tile, which
drop when the texture leaves; the removed component's own large-scale figure, which missed the
Nikkor's ring), the picks on the five masters and the known limits (lens versus source, a
corner-only feature, the five-master basis of the texture threshold, the `W/47` box on small
fixtures and the 13-px weave of the golden fixture) are ADR 0027.
