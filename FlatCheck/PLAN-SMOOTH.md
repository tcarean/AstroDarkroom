# Plan: a sixth mode - the smoothed flat ("Netezire" tab, `--smooth`)

**Status: implemented (2026-09-16).** Kept as the record of what was measured and why the
two rejected designs were rejected; the C# port reproduces the Part 0 prototype figures on
the five real masters to the second decimal. Two details differ from the plan as written:
the scale is the kernel's **sigma** in sensor pixels (300 px), not a "diameter", and the
smoothing tab's structure batteries are shown as two stacked cards rather than two columns.

Read `CLAUDE.md` first. Everything below obeys it: nothing is computed or worded outside
`FlatCheck.Core`, no version bump, no commit, GPL header on every new file, user-facing
text in Romanian, code documentation in English. **No Python parity**, by decision, for
the same reason as `--histeq`: the product is a file, not a number `crosscheck.py` could
compare. The numpy prototype that chose the method (Part 0, below) lived in the
scratchpad and is not committed; its results are.

The plan is written so that each numbered step is a self-contained unit of work with its
own tests; the order is in Part 6. A step that says "new file" also means "GPL notice
first" - `LicenseHeaderTests` walks every project and fails otherwise.

## What the mode is for

A master flat shot through a fabric diffuser carries the diffuser's texture. The texture
is world-fixed and small-scale; the lens's vignetting is camera-fixed and large-scale. A
purely radial synthetic flat would drop the texture but also drop every real, non-radial
thing the lens does - and on the Tokina 20 mm the four corners differ by 6-16 % (see the
measurements below), which is exactly the case where a radial model corrects worse than
the flat it replaces.

So the mode keeps **everything smooth** and drops **everything else**: it writes a second
master, `<name>_netezit.fit`, that is the input's large-scale structure with the texture,
the dust shadows and the blotches removed. It is a *file-producing* mode, like `--histeq`
is a picture-producing one, and it has no verdict of its own. What it does report is
what it removed, as a map and as figures, because that is the one thing the user must
look at before trusting the file: if the removed component contains large-scale
structure, the smoothing ate something real.

**It cannot be validated by the structure mode**, and the report says so: the smoothed
flat's five deviation tests are the same as the input's minus the texture. The honest
check is `--light` on a calibrated light series, and on a 20 mm lens that is refused
(field ~70°, guard at 40°). The remaining check is the removed-component map, at a
fixed ±2 % scale, plus the same smoothing run on a second master of the same lens: two
smoothed masters of one lens must agree up to a low-order illumination gradient.

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

1. **Global polynomial over the residual of a ring-median radial model** (degree 2-10).
   Failed: every master carries mid-scale structure - the monitor's own blotches at
   ~1000-1500 px and ±3 % on tokina_aproape, a decentred ring on the Nikkor, LCD blobs at
   ±0.7 % on the Samyang - that a degree-3 polynomial cannot follow (tile RMS after the
   fit 0.5-1.7 %, decreasing to degree 10 with no floor) and that a degree-6+ polynomial
   follows by oscillating in the corners (kept non-radial ptp jumping to 22 %, worst tile
   10 %). "Large-scale structure is low-order" was wrong, and a polynomial basis has no
   scale knob.
2. **Ring-median radial model x local-linear field (LOESS-1) on the tile grid.** Good
   figures, but the **radial model puts rings into the file**: beyond the inscribed
   circle a ring is partial arcs, any even non-radial structure makes the ring
   distribution bimodal and the median jumps bin to bin - the artefact `CLAUDE.md`
   documents for the map, which the symmetrisation cures only for the odd part. The
   field, at a kernel of 150 px, cannot absorb 22 px rings, so they survive into the
   output. Measured: ring-profile amplitude of the removed component ±0.11-0.13 % on
   tokina_pemonitor, visible as concentric circles in the ratio of two smoothed masters;
   smoothing the 1-D profile did not remove them (the jumps are below the profile
   smoother's scale and above the field's). A radial model of any kind is the wrong
   tool here: **the split between "radial" and "the rest" is arbitrary, and the only
   requirement is that the output be smooth and follow the data.**
3. **No radial model. Tile medians, local quadratic (LOESS-2), robust, evaluated per
   pixel.** This is the design. It has no rings by construction (nothing in it knows
   about a centre), follows the vignetting's curvature to the corner (a quadratic over a
   ±450 px window of a profile whose scale is ~1800 px is exact to third order), and
   its one knob is a length scale. Two details cost a round each and are recorded so
   they are not rediscovered:
   - **The clipping needs an absolute floor.** Relative to the MAD alone, a very clean
     master (Samyang, tile RMS 0.06 %) had 8 % of its tiles rejected, because anything
     the local quadratic does not follow at 0.15 % became an "outlier" and was then not
     followed at all. With `max(2.5 MAD, 2 % of the level)` nothing is rejected on the
     four clean masters and 57-69 tiles per position on tokina_pemonitor - the two dust
     spots, the bright spot, and the deepest cells, which are exactly the things that
     should go.
   - **Partial tiles must sit where they are.** The grid keeps the partial last row and
     column (58 rows and 8 columns on this sensor) so nothing is extrapolated; but the
     first cut fitted them one full tile from their neighbour, and the synthetic
     showed a 0.8 % error band along the right edge and 0.2 % along the bottom. With
     the real centre offsets in the fit the whole-frame maximum error on the clean
     synthetic went from 1.05 % to 0.19 %.

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

The monitor's blotches (±3 % at 1000-1500 px on tokina_aproape), the sky gradient of a
sky flat, the corner inequality of a non-uniform source - everything smooth stays,
because the pixels do not say whether it is the lens or the source. This is the same
limit the structure mode has, and `--ratio` is still the only thing that separates the
two. The mode removes texture, dust and blotches; it does not remove illumination.

## Decisions (taken here; reopen only with a measurement)

1. **Model = local quadratic on the tile-median grid, per Bayer position, at the position
   raster's full resolution.** No radial model, no symmetrisation, no centre - see Part 0
   for why the two designs that had one failed. `SmoothAnalyzer` holds `SigmaTiles = 2`,
   `ClipSigma = 2.5`, `ClipFloor = 0.02`, `ClipRounds = 3` as named constants with the
   measurement beside each.
2. **Per position, never across the mosaic.** The four Bayer positions have different
   levels and slightly different vignetting; any filter that mixes them writes a
   chequerboard. `Mosaic.Split` exists; `Mosaic.Merge` is its inverse and is new. Mono
   (by `Mosaic.LooksCfa`, the rule `--expo` and `--histeq` use) is one position covering
   the frame. **Nothing is guessed about colour order**: the mode never needs it.
3. **The scale is the one knob.** `--scale <px>` on the console, in sensor pixels
   (default 300 = `SigmaTiles · TileSize · 2`); the window offers three chips, 150 / 300 /
   600 px, default 300, on the `MapScaleViewModel` pattern. The report always prints the
   scale used, because the file's meaning depends on it.
4. **The output is the input's type and scale.** Float in, `BITPIX -32` out at the same
   values (Siril's 0..1 master stays 0..1); 16-bit in, `BITPIX 16` with `BZERO 32768`
   out. No renormalisation. `BITPIX 8`, `32` and `-64` inputs are written as `-32`.
5. **The header travels.** `FitsReader` keeps the raw 80-character cards of the HDU it
   read (`LoadedFrame.Cards`, empty for a raw), and `FitsWriter` copies every card
   except the structural ones it rewrites (`SIMPLE`, `BITPIX`, `NAXIS*`, `EXTEND`,
   `BZERO`, `BSCALE`, `END`), then appends `HISTORY` cards naming the tool, the version
   and the scale. `BAYERPAT`, `XBAYROFF`/`YBAYROFF`, `DATE-OBS`, `STACKCNT` arrive
   verbatim. The parsed dictionary is *not* the source: it loses comments and truncates
   strings at `/` (known issue 1).
6. **The mode refuses two inputs and warns on one.** A camera raw is refused (a single
   uncalibrated frame; `--expo` is the mode for a raw). A colour cube (`NAXIS3 > 1`) is
   refused, because `FitsReader` averages the planes and writing that back as mono would
   silently discard two channels. Both are refusals in the `--light` sense: no file, exit
   1. The warning is on the *output*: when the removed component's worst tile exceeds
   `Th.SmoothKeptWarn` (**2 %**, `Th.ResidPerfect` - on the four clean masters the worst
   tile is 0.40-0.71 %, on the textured one 4.2 %, so the margin is 3x either way), the
   report says the smoothing removed more than texture, names the tile, and leaves the
   decision to the user.
7. **Two maps, both centred on 1, so `PngWriter`'s pin at 1.0 holds.** Primary: the
   removed component - the one thing on the screen that says whether the file is
   trustworthy. Secondary: the smoothed flat's own residual after its radial model, from
   the "after" `StructureResult`. Window default cap for the primary map is ±2 %
   (`Th.ResidPerfect`), not `PngWriter.DefaultCapPct`: what is removed is small.
   `MapScaleViewModel` takes the default as a constructor argument for this.
8. **The report is "before / after", not a verdict.** `StructureAnalyzer.AnalyzePrepared`
   runs on both prepared rasters and the report prints the five tests side by side. The
   mode's own figures are about the removed component: p1/p99 at pixel level,
   `LargeScalePtp`, `MaxTileDeviation` and `LocalSpots` on it, tiles rejected per
   position, and the scale. Exit code 0 when the file was written, 1 when it could not
   be - the `--histeq` rule.
9. **Console: `--smooth <iesire.fit>` is a value flag; `-o` stays the map.** Explicit
   output path, no default naming on the console. It refuses to overwrite the input. The
   window builds the path itself, `<master dir>/pp_flat_stacked_netezit.fit`, and asks
   before overwriting. That name does not match `SirilSession.FlatMasterCandidates`
   (exact paths), so the session's master stays the shot one and the Structure tab keeps
   showing the texture - the smoothed file must never hide the problem it was made for.
   It *does* match `RatioPattern` (`pp_flat_stacked*.fit`), which is right.
10. **The window runs it on a button, not on open.** A file is not something to produce
    because a folder was looked at. `Netezeste si salveaza` runs the chain, shows the
    maps and the before/after rows, writes the FITS, and prints the saved path with the
    sentence that Siril's scripts will not pick it up by themselves. It runs after the
    structure run, not beside it - the sequential rule in `MainViewModel`.

## Part 1 - Core

### 1.1 `FlatCheck.Core/Mosaic.cs` - `Merge`

`internal static Img Merge(Img[] positions, int h, int w)`: the inverse of `Split`, into
a raster of the *original* size. `Split` drops an odd last row or column; `Merge` fills
one by copying the nearest even row or column. `MosaicTests`: `Merge(Split(a))` is `a`
on an even frame, and on an odd one differs only in the last row/column.

### 1.2 `FlatCheck.Core/Img.cs` - `TileMedians`

`TileMedians(Img, k) -> (Img tiles, double[] cy, double[] cx)`: `ceil(h/k) x ceil(w/k)`
tiles, **partial last row and column kept**, the real centre of every tile returned.
Deliberately not `BlockMean`'s grid, and the comment says why: the smoothing must reach
the last pixel, and a tile fitted one full step from its neighbour when it is half a
step away put a 0.8 % band along the edge. Uses `Stats.Percentile(double[], 50)` on a
per-tile buffer reused across tiles. `ImgTests`: shape, centres, a tile holding one
-50 % pixel returns the typical value, the partial tile's centre is its own.

### 1.3 `FlatCheck.Core/FitsReader.cs` and `ImageLoader.cs` - the cards and the plane count

`LoadedFrame` gains `IReadOnlyList<string> Cards` (the raw 80-character cards of the HDU
the image came from, `END` excluded, empty for a raw) and `int Planes` (`NAXIS3`, 1 for a
2-D image; a raw reports 1). `LoadWithHeader` keeps the cards alongside the dictionary.
Nothing else changes. `FitsReaderTests`: the cards come back verbatim including a
`COMMENT` with a slash in it, and a cube reports `Planes == 3`.

### 1.4 `FlatCheck.Core/FitsWriter.cs` (new file)

`internal static void Write(Stream s, Img img, int bitpix, IReadOnlyList<string> cards,
IReadOnlyList<string> history)`. Writes `SIMPLE`, `BITPIX`, `NAXIS = 2`, `NAXIS1/2`,
`EXTEND = T`, then `BZERO`/`BSCALE` for `BITPIX 16` (32768 / 1), then every input card
whose keyword is not structural, then one `HISTORY` card per history line (wrapped at 72
characters), then `END`, padded to 2880; then the data big-endian, `-32` as IEEE single,
`16` as `short` after subtracting `BZERO` with rounding and clamping, padded to 2880. A
`Stream`, not a `byte[]`, for the same reason the report is a `TextWriter`: 96 MB of
float need not sit in memory a second time.

Card formatting follows the standard: keyword left-justified in 8, `= ` at 9-10, logical
and numeric values right-justified to column 30, strings quoted from column 11 with `'`
doubled. Only the cards the writer *generates* are formatted; copied cards are copied.

`FitsWriterTests` (new): a round trip through `FitsReader.LoadWithHeader` returns the same
pixels bit for bit for `-32`, and to ±0.5 ADU for `16`; the round trip's header carries
the copied `BAYERPAT` and no duplicated `NAXIS1`; the file length is a multiple of 2880;
astropy, when present, opens it (a `SirilCliIntegrationTests`-style test that passes
vacuously without Python).

### 1.5 `FlatCheck.Core/Analysis/SmoothAnalyzer.cs` (new file)

`SmoothResult`: `Img Smoothed` (full resolution, the input's shape); `bool IsCfa`;
`double ScalePx`; per-position `PositionFit[]` (tiles, rejected, tile RMS after the fit);
the removed component at analysis resolution (`Img Removed = Prepare(input) /
Prepare(smoothed)`, its p1/p99, `RemovedPtpPct`, `RemovedMaxTilePct`, `RemovedSpotPct`
+ position, `RemovedFlagged`); `StructureResult Before, After` (both through
`AnalyzePrepared`); `int Width, Height` of the analysis raster.

`Analyze(Img input, double scalePx = DefaultScalePx)`: `Mosaic.Split` when `LooksCfa`
on the four position medians (from a 65536-bin histogram as `HistEqLut.Median` does,
not from a sort), else one position; for each position, in turn: `TileMedians` ->
`LocalQuadratic` (the LOESS-2 with real offsets, 3 clipping rounds, floor) ->
`Evaluate` (the bilinear blend of four quadratic predictions) -> store through `Merge`.
`sigma_t = scalePx / (2 · k)` so the scale is in sensor pixels whatever the tile. The
position's temporaries are dropped before the next one starts. The 6x6 systems go
through `Stats.Solve`; a singular one (a position that is all rejected tiles) falls
back to the tile median, marked `FitFailed`, reported, not thrown.

Tests (`SmoothAnalyzerTests`, new), on the Part 0 synthetics scaled to 1024 x 683:
`AVignettedGradientIsRecoveredToUnderATenthOfAPercent_CornersIncluded` (the floor);
`ThePartialLastTileSitsWhereItIs` (an odd-sized frame shows no error band along the
last column - the 0.8 % lesson); `CellsAndDustAreRemoved_TheGradientAndBlobsAreKept`;
`TheClipFloorKeepsSmoothStructureOnACleanField` (no tile rejected on a field with 0.5 %
blobs); `TheOutputHasNoChequerboard` (four position levels match the input's to 0.1 %,
2x2 spread unchanged); `NothingIsRenormalised`; `RemovedIsCentredOnOne`; mono goes
through one position; a constant frame does not throw; `ScaleIsInSensorPixels`.

### 1.6 `FlatCheck.Core/Report/SmoothReport.cs` (new file)

`Write(SmoothResult, path, TextWriter)`: header `=== NETEZIRE: <path> ===`, the input
line (size, CFA or mono, type), the model line (scale in px, tiles, rejected per
position), the `CE S-A SCOS` block (p1/p99, ptp, worst tile, deepest spot with sign
words via `StructureReport.LocalSpotValue`), the warning when `RemovedFlagged`, then
`INAINTE / DUPA` - the five tests in two columns with their verdicts - then the fixed
caveat (what the file keeps that may not be the lens; what it drops that may have been
needed: dust for a same-session flat; a ~300 px real feature is partly taken, and the
scale chip is for that), and the line about Siril not picking the file up by itself.
`SavedLine(path)` for the FITS.

As values, for the window: `Caveat()`, `ScaleLine(r)`, `RemovedLines(r)`, `Warning(r)`
or null, `SirilNote()`. `SmoothReportTests`: the warning appears iff flagged and quotes
`Th.SmoothKeptWarn`; every value the window binds is non-empty; the before/after rows
carry the structure labels verbatim.

### 1.7 `FlatCheck.Core/Runs/SmoothRun.cs` (new file)

`Execute(string path, double scalePx, TextWriter report) -> SmoothOutcome`:
`ImageLoader.Load`; refuse a raw (`Cards.Count == 0`) and a cube (`Planes > 1`) with the
messages in `SmoothReport.RefusalRaw/RefusalCube`, returning `Refused = true` and no
result - the `LightRun` shape; else `SmoothAnalyzer.Analyze`, `SmoothReport.Write`,
`Png = EncodeResidualMap(Removed)`, `SecondaryPng = EncodeResidualMap(After.Residual)`.
`WriteFits(SmoothOutcome, LoadedFrame, Stream)` writes the file through `FitsWriter`
with the input's cards and the history lines. `OutputBitpix(inputBitpix)` holds
decision 4, tested.

`RunsTests`: the same-numbers-as-the-analyzer and same-text-as-the-report pairs; the PNG
is decodable and sized like the analysis raster; a cube fixture (`BuildFitsCube`) is
refused with the cube message and `Result == null`; the written FITS re-loads to
`Smoothed` bit for bit.

### 1.8 `FlatCheck.Core/Thresholds.cs`

`Th.SmoothKeptWarn = ResidPerfect` (2 %; measured margin in Part 0). Nothing else: the
kernel, the clip level and the floor are model parameters and live in the analyzer.

## Part 2 - Console

### 2.1 `FlatCheck/Options.cs`

`--smooth <iesire.fit>` and `--scale <px>` in `ValueFlags`. Refuses an empty output, an
output equal to the input (`Path.GetFullPath` on both, case-insensitive on Windows), and a
scale outside 50-2000 px. `OptionsTests`: flag before and after the file; the refusals;
`--scale` without `--smooth` parses and is ignored.

### 2.2 `FlatCheck/FlatCheckCli.cs`

Dispatch order becomes `--smooth` -> `--histeq` -> `--expo` -> `--ratio` -> `--light` ->
structure. `RunSmooth`: `SmoothRun.Execute`; on refusal return 1 without writing; else
`FileStream` at `o.Smooth`, `WriteFits`, print `SavedLine`, save the two maps (`-o` and
a `-dupa` suffix through `AfterMapPath` beside `WorldMapPath`), return 0. `WriteUsage`
gains the lines.

## Part 3 - Window

### 3.1 `FlatCheck.Wpf/Session/SirilSession.cs` and `SessionMessages.cs`

`SmoothedMasterPath` = the found master's directory + `pp_flat_stacked_netezit.fit`
(`SmoothedSuffix = "_netezit"`). `SessionMessages`: the tab's explanation rows (what the
file keeps, what it drops, the scale, when to use it, how to make Siril use it), the
overwrite question, the no-master message reused from Structure.

### 3.2 `FlatCheck.Wpf/ViewModels/SmoothTabViewModel.cs` (new file)

Over `TabViewModel`. `Attach(session, ct)` stores the master path and shows the
explanation with the button enabled; nothing runs. `Scale` is a `SmoothScaleViewModel`
(three chips, `GroupName` `SmoothScale`, on the `MapScaleViewModel` pattern; decides
nothing). `RunAsync()` (the button): `RunGuardedAsync` -> `Task.Run(SmoothRun.Execute)`
-> on refusal `Fail`; else `Populate(result)`: `Before`/`After` as two replaceable
`IReadOnlyList<MetricRow>` from `StructureRows.Tests`, `Scale`, `Removed`, `Warning`,
`Caveat`, `SirilNote`; the two maps behind a radio toggle (`GroupName` `SmoothMapKind`),
`MapScaleViewModel` defaulting to `Th.ResidPerfect` with `GroupName` `SmoothMapScale`,
the legend; the raster kept for re-encoding per cap, cached. Then the FITS is written on
the pool thread (`File.Exists` -> the overwrite question through an injected
`Func<string, bool>` so tests never see a dialog) and `SavedPath` is set. `Populate` is
`internal` for the smoke tests.

### 3.3 `FlatCheck.Wpf/Views/SmoothView.xaml` + `.xaml.cs` (new files, GPL comment block)

Left column: the explanation (expanded by default - it is the tab's precondition, like
Ratio's), the scale chips, the button, the saved-path card, the warning card
(`VerdictPalette` colour of `ACCEPTABIL`: a caution, not a verdict), the `CE S-A SCOS`
rows, the `INAINTE / DUPA` battery as two `MetricRowTemplate` lists. Right column: the
map with its kind toggle, cap chips and legend. `BusyOverlay`, `MessagePanel`,
`ReportPanel` as everywhere.

### 3.4 `FlatCheck.Wpf/MainWindow.xaml` and `ViewModels/MainViewModel.cs`

Sixth `TabItem`, `Netezire`, after `Histogram`. `MainViewModel.Smooth`; `OpenAsync` calls
`Attach` and nothing else; folder change cancels a run in flight. `RunAsync` awaits the
session's current chain first (`WhenIdleAsync`, one line, tested) so it never overlaps
the structure run.

## Part 4 - Tests

- `FlatCheck.Tests`: `MosaicTests` (1.1), `ImgTests` (1.2), `FitsReaderTests` (1.3),
  `FitsWriterTests` (1.4, new), `SmoothAnalyzerTests` (1.5, new), `SmoothReportTests`
  (1.6, new), `RunsTests` (1.7), `OptionsTests` (2.1).
- `GoldenOutputTests` + `Golden/smooth_clean.txt`, `Golden/smooth_textured.txt`,
  `Golden/smooth_refuses_cube.txt`: the console text with the FITS path scrubbed like the
  PNG path; plus assertions that the FITS exists, re-loads, and has the input's shape.
- `VersionTests`: `--smooth` in the every-mode banner and copyright lists (success and
  refusal).
- `FlatCheck.Wpf.Tests/SmoothTabTests.cs` (new): `Attach` runs nothing and enables the
  button; `RunAsync` writes the file where `SmoothedMasterPath` says and fills both
  batteries; the overwrite question is asked when the file exists and a `false` answer
  writes nothing and still shows the result; a cube master fails with the cube message;
  the ±2 % cap is selected by default; the scale chips change `ScalePx`; folder change
  mid-run leaves the tab empty.
- `WindowSmokeTests`: the tab rendered empty, after a run, and refused; no binding
  errors. The tab-count heading assertion, if any heading quotes it.
- `MainViewModelTests`: the sixth tab resets with the others; a click during the
  structure run waits for it.

## Part 5 - Documentation

### 5.1 `README.md`

A sixth section under the modes, in Romanian: what the file is, the Part 0 measurement
in two paragraphs (the cells, why not a radial model, why not a window), what it keeps
that may not be the lens, what it drops that may have been wanted, the scale and its
three values, the `--smooth <iesire.fit> [--scale <px>]` line in the usage block, the
`-dupa` map, exit codes, how to make Siril use the file, and the acceptance check the
user can run themselves (two smoothed masters of one lens through `--ratio`). The tab in
the window table.

### 5.2 `CLAUDE.md`

The mode under "The five modes" (six, dispatch order updated), `FitsWriter.cs` and
`SmoothAnalyzer` in "Structure", the Part 0 findings under their own heading - the two
failed designs in two sentences each, so nobody re-tries a radial model here - decisions
2, 3, 5, 9 and 10 where a future reader would trip over them, the sixth tab in the
two-applications table, `Th.SmoothKeptWarn` in the thresholds table, the Python
divergence line.

### 5.3 Version

Not bumped. A new mode with a new output file is the user's call for a minor bump.

## Part 6 - Order and independence

1. 1.1, 1.2, 1.3, 1.4 - independent of each other and of the analyzer; each with tests.
2. 1.5 (needs 1.1, 1.2), then 1.6, then 1.7 (needs 1.4), then 1.8 alongside 1.5. Before
   moving on, run the C# analyzer on the five Part 0 masters and compare the removed
   component's figures with the table above - they must agree to the second decimal, or
   the port differs from the prototype somewhere.
3. Part 2, then the golden files and `VersionTests`.
4. Part 3, then the window tests and the smoke render; look at a
   `FLATCHECK_DUMP_SCREENS` render of the tab before calling it done.
5. Part 5 last, with the measured figures in it.

## Measurements to take while implementing (write them into the code comments)

- The C# removed-component figures on the five masters against the Part 0 table.
- Peak memory and time on the 24 MP masters, per position and total (Release).
- `FitsWriter` output opened in Siril 1.4.4 as a flat in `calibrate`, once, by hand, and
  the calibrated light compared with one calibrated by the original master.
