# FlatCheck

Quantitative diagnostic for astrophotography flat frames. Answers four questions:
is the master flat radially symmetric, is the flat *setup* clean, does the flat
actually correct a real light, and is a raw flat exposed correctly.

Small C# console app, **zero NuGet dependencies**: it hand-parses FITS and
hand-writes PNG (via `ZLibStream` from the BCL). .NET 8.

## Structure

- `FlatCheck/` - the app. `AssemblyName` is `flatcheck`, so the binary is
  `flatcheck.exe`, not `FlatCheck.exe`.
  - `Program.cs` - the usage documentation, and a one-line `Main` shim.
  - `FlatCheckCli.cs` - `Run(args, stdout, stderr)`: mode dispatch and exit code.
    All output goes through the injected `TextWriter`, never `Console` directly.
  - `Options.cs` - CLI parsing.
  - `FitsReader.cs` - the hand-rolled FITS reader. `Load(byte[])` is the real
    entry point; `Load(string)` is a thin file wrapper, which is what lets the
    reader be tested without touching disk.
  - `Thresholds.cs` - `Th`. Note it does **not** hold every threshold; see below.
  - `Img.cs` - the 2-D raster plus its pure transforms: `BlockMean`, `Prepare`,
    `Divide`.
  - `Stats.cs` - numeric primitives: `Percentile`, `RobustPtp`, `Interp`, `Solve`,
    `Verdict`, `Rank`.
  - `Core.cs` - what is left of the engine: metrics (`FitCenter`, `RadialModel`,
    `CornerStats`, `AzimuthalAsymmetry`, `LargeScalePtp`, `TileBackground`), the
    four modes, and the PNG encoder. Phases 5-7 split this further.
- `FlatCheck.Tests/` - xUnit tests, reaching the app via `InternalsVisibleTo`.
  `Golden/` holds the characterization snapshots.
- `flatcheck.py` - the original Python/numpy/astropy prototype the C# tool was
  ported from. Prior art only; **not maintained in parallel** - don't feel
  obliged to mirror C#-side changes into it. It is, however, the reference for
  *numeric* behaviour: see "Parity with Python" below.
- `README.md` - user-facing docs, in Romanian.

Phases 0-4 of [REFACTOR-TESTS.md](REFACTOR-TESTS.md) are done; phases 5-8 are not.

## Conventions

- **User-facing text (CLI messages, `--help`, code comments) is in Romanian** -
  keep new text consistent with this. Documentation *about* the code
  (this file, the refactoring plan) is in English.
- **No external dependencies.** The FITS reader and PNG encoder are deliberately
  hand-rolled. Don't add a NuGet package to replace them without a strong reason;
  the point of this tool is that it is a single copyable binary.
- **Every metric is a ratio**, so ISO/gain cancels out. Don't add gain-dependent
  corrections - if a new metric isn't scale-invariant, that's a design smell.
- **Thresholds are empirical rules of thumb, not physical constants.** They are a
  starting point to be calibrated against a known-good setup.

## The core idea

For a photographic lens with no optical train (the author's setup is a Samyang
135 f/2 on a Nikon D3300), a *correct* flat is by construction a **purely radial**
function centred on the optical axis. Vignetting is rotationally symmetric; dust
at f/2 is below threshold.

Therefore any departure from radial symmetry is **not vignetting - it is error**
(non-uniform light source, light leak, tilted camera, ambient light). "Good flat"
does not mean "looks flat"; it means "the departure from radial symmetry is below
noise". That is a number, and it can be measured.

A crucial consequence, and an easy thing to break: the radial model and the
azimuthal asymmetry are computed around the **sensor centre** (`w/2, h/2`), *not*
around the fitted illumination peak. Centring on the fitted peak would let the fit
absorb exactly the linear gradient you are trying to detect (a gradient laid over
quasi-quadratic vignetting merely shifts the vertex), and the test would silently
cancel itself out. `FitCenter` is used *only* to report the offset as its own
metric.

## Input contract

Modes 1-3 want a **calibrated** FITS - flat-dark/offset already subtracted. In the
Siril workflow that is `process/pp_flat_stacked.fit` (the `pp_` prefix means
calibrated). An uncalibrated stack still has a ~150-600 ADU pedestal which
flattens the profile and under-reports vignetting by ~1%, and **all tests will
report OK anyway**, because the profile stays radially symmetric - only the
amplitude is wrong. Not detectable, only avoidable.

Mode 4 (`--expo`) is the exception: it wants a **raw, uncalibrated, single**
frame with the CFA mosaic intact and the original ADU scale, because stacking
normalises away the absolute level. In Siril that means the **Conversion** tab
(add NEFs → Convert), *not* Open + Save, which debayers and normalises. The mode
detects and rejects both failure modes (see "Raw-file sanity gate").

Accepted formats: FITS only (`.fit`/`.fits`/`.fts` - though the extension is
never actually checked, see Known issues). BITPIX 8/16/32/-32/-64, mono or
colour. The Python version could additionally read NEF directly via `rawpy`; the
C# port cannot, by design.

## The four modes

Dispatch order in `FlatCheckCli.Run` is `--expo` → `--ratio` → `--light` → structure
(default). The modes are mutually exclusive; the first match wins.

### 1. Structure (default) - `flatcheck flat.fit`

Is the master flat radially symmetric? Runs on the final master.

1. `FitsReader.Load` → `Prepare` (2×2 block-mean, then downsample toward ~1200 px).
2. `FitCenter` → illumination peak; reported as offset from sensor centre, as a
   percentage of the half-diagonal.
3. `CornerStats` → median of four 5% corner boxes and a central box. Reports
   vignetting in stops (informative only, *not* a test) and the spread between
   the four corners (a test - the corners should all be equal on a radial field).
4. `RadialModel` around the sensor centre → residual `img / model`, then
   `LargeScalePtp` of that residual (tile-averaged, inside the inscribed radius).
5. `AzimuthalAsymmetry` → per-ring peak-to-peak across 12 sectors; the max is the
   metric.
6. Verdict = the worst of the four tests. Writes the residual heat map PNG.

### 2. 180° rotation - `flatcheck A.fit --ratio B.fit`

Validates the **setup**, not the master. Shoot ~15 flats, rotate the camera 180°
about the optical axis, shoot ~15 more, stack the two sets separately.
Vignetting and dust are fixed *on the sensor* so they cancel in A/B; any external
gradient is fixed *in the world* so it inverts, and shows up in A/B at ~2×
amplitude. Hence "the real error in each master is about half these numbers".

Computes `A/B` normalised to median 1, then `LargeScalePtp` over the whole frame
(the inscribed radius is passed as the full diagonal, i.e. no masking) and the
corner spread. Tighter thresholds than mode 1, because the ratio should be
featureless.

**Known blind spot**, documented in the source: contamination that is itself
radially symmetric about the optical axis (e.g. a monitor backlight falloff
centred on the lens) survives the rotation and is reported clean. The counter-test
is a *distance* test - one set at 30 cm, one at 60 cm, `--ratio` between them,
since lens vignetting is distance-invariant and panel contamination is not.

### 3. End-to-end - `flatcheck light.fit --light`

The only mode that closes the loop; 1 and 2 are proxies. Input is a light frame
or stack **calibrated with the flat but with no background extraction**.

`TileBackground` takes the 15th percentile inside each tile (a low percentile is
robust to stars and nebulosity, which only add signal), giving a local background
estimate. It then fits, simultaneously:

```
bg = a + b·X + c·Y + d·R² + e·R⁴          X, Y normalised by the half-diagonal
```

The plane (`b`, `c`) absorbs the light-pollution gradient; the radial terms
(`d`, `e`) capture residual vignetting, i.e. flat error. Fitting both at once is
what makes the test work under a polluted sky - the two have different shapes and
separate cleanly. The source carries an explicit warning: **do not** substitute
percentiles on circular annuli, because p20 on a ring picks up the dark side of
the sky gradient and reports it as false vignetting.

Four iterations of one-sided sigma-clipping (`res < 2.5σ`, σ from the MAD ×
1.4826) reject tiles containing nebulosity. One-sided is deliberate: contaminants
are positive.

The reported figure is `radPct = -(d + e)/a × 100` (R² = 1 at the corner). **The
sign is the diagnosis:**

- `radPct > 0` - background **falls** toward the edges → **under-correction**
  (different aperture at flats than at lights, or contaminated flat).
- `radPct < 0` - background **rises** toward the edges → **over-correction**,
  almost always a missing/wrong flat-dark: you divided by a flat that still
  contains an offset.

Thresholds here are hardcoded inline (2.0 / 5.0), not in `Th`.

### 4. Exposure - `flatcheck flat_brut.fit --expo [--sky]`

Splits the frame into the 4 Bayer positions (`ch[(y&1)*2 + (x&1)]`) and takes the
median of each. If `max/min > 1.15` the file is treated as CFA and the brightest
channel is used (the brightest channel clips first, so it is the limiting one);
otherwise the frame is treated as mono/processed and the whole frame is used.

The decision metric is the median of a **central 20% × 20% window** of the
brightest channel, **not** the global median. This is the single most important
design point of this mode: a flat has a wide distribution *by definition* - that
is what vignetting is - so the global median is dragged down by the corners. Follow
the global median and you will overexpose until the centre clips, which destroys
vignetting information exactly where the signal is highest. The global median is
still printed, but labelled informative.

`white` is auto-detected from the frame max (≤1.5 → 1.0; ≤4300 → 4095; ≤17203 →
16383; else 65535) and can be overridden with `--sat` / `--black`. To find the
true white level: shoot one deliberately overexposed frame; its max *is* saturation.

**Raw-file sanity gate.** Before issuing a verdict, the mode rejects the file if
either `cenMed < 1.0` (level near zero → already calibrated or normalised) or the
CFA spread `< 1.02` (the four Bayer positions are near-identical → the file is
debayered). This catches the common "opened the NEF in Siril and hit Save"
mistake. When it fires, the mode prints an explanation and returns **without a
verdict**.

Verdict bands on the central window (all hardcoded local constants):

| condition | verdict |
|---|---|
| clipping > 0.05% or > 80% | PREA SUS |
| < 30% | PREA JOS |
| < 50% | ACCEPTABIL, dar cam jos |
| 50-65% | **OK** |
| 65-80% | ACCEPTABIL, dar cam sus |

With `--sky` the bands change to 15-50% OK, > 60% (or clipping) too high, < 15%
too low. Sky flats are *supposed* to sit modestly: the blue-hour sky darkens
continuously and is full of faint stars, so master SNR comes from the *number* of
frames, not from pushing one frame to 55%. Without `--sky` a sky flat at 22% would
be wrongly reported as underexposed.

Suggested corrections are `log2(57/cenMed)` stops - 57 being the midpoint of the
50-65 target band.

## Shared pipeline and algorithms

**`Prepare(data, targetLong = 1200)`** - `BlockMean(data, 2)` then
`BlockMean(img, k)` with `k = max(1, (int)(max(H,W) / 1200.0))`.

The 2×2 block-mean is what makes the tool work directly on an undebayered CFA
master: each 2×2 RGGB block contains exactly 1 R, 2 G, 1 B, so it is a *fixed*
linear combination and the radial profile survives intact. This is why you can
feed it `pp_flat_stacked.fit` straight out of Siril.

Note the integer truncation of `k`: the ~1200 px target is loose. A 6000×4000
D3300 frame becomes 3000×2000 after the 2×2, then `k = (int)(3000/1200) = 2` →
**1500×1000**, not 1200. Anything landing between 1200 and 2400 is left alone.
This matches Python exactly, so it is parity-correct - just don't be surprised.
`BlockMean` truncates partial blocks (`a.H / k`), dropping the last rows/columns.

**`Percentile(list, p)`** - sorts a copy and interpolates linearly between the two
neighbouring order statistics, matching `numpy.percentile`'s default. Returns 0 on
an empty list (numpy would raise - a deliberate divergence, but it means a
degenerate input yields a silent 0 rather than an error).

**`Solve(A, b, n)`** - Gauss-Jordan with partial pivoting; returns `null` if
singular (pivot < 1e-15). Used for both least-squares fits, which are set up as
**normal equations** (`AᵀA x = Aᵀb`) accumulated in-place rather than via a QR/SVD
`lstsq`. This is less numerically stable than numpy's `lstsq` but adequate for
6- and 5-parameter fits on well-conditioned normalised coordinates.

**`FitCenter(img)`** - fits a paraboloid to `log(I)` over
`[1, xn, yn, xn², yn², xn·yn]` on a strided subsample (`step = max(1, min(h,w)/200)`),
then solves for the vertex via `M = [[2d, f], [f, 2e]]`. Falls back to the geometric
centre if the system is singular or the vertex lands outside the frame.

**`RadialModel(img, cx, cy, nbins = 80, pct = 50)`** - bins pixels by radius into
80 equal-width bins out to `rmax`, takes the median per bin (bins with < 8 pixels
are dropped), then rebuilds a 2-D model by linear interpolation of that profile.
`Interp` clamps at both ends, matching `np.interp`.

**`AzimuthalAsymmetry(img, cx, cy, nsect = 12, nrings = 10)`** - 10 rings spanning
`0.15·r_ins` to `r_ins`, 12 angular sectors. Per ring: median of each sector, then
`(max - min)/median × 100`. Restricted to the **inscribed** radius because only
there is every azimuth sampled - out in the corners the comparison would be
invalid. Rings needing at least `nsect - 1` populated sectors.

**`LargeScalePtp(resid, cx, cy, rIns, ntiles = 40)`** - block-means the residual
into ~40-wide tiles, keeps tiles whose centre is inside `rIns`, and returns
`p95 - p5`. The tile averaging is essential: noise falls as `√(px/tile)` while
structure survives. Without it the metric would measure noise, not error.

**Verdict ladder** - `Verdict(val, ok, bad)` → `OK` / `SUSPECT` / `PROBLEMA`;
`Rank` maps those to 0/1/2 and the mode verdict is the worst of its tests. Note
that `NaN` compares false against both bounds and therefore degrades to
**PROBLEMA** - which is how degenerate inputs (too few tiles, no valid rings)
surface. Matches Python.

## Thresholds

`Th` holds some but not all of them; the rest are inline. Full picture:

| metric | mode | OK < | PROBLEMA ≥ | defined in |
|---|---|---|---|---|
| corner spread | structure | 1.5% | 4.0% | `Th.CornerOk/Bad` |
| azimuthal asymmetry (max) | structure | 1.5% | 4.0% | `Th.AzimOk/Bad` |
| residual after radial model | structure | 1.5% | 4.0% | `Th.AzimOk/Bad` (reused) |
| illumination-centre offset | structure | 4.0% | 10.0% | `Th.CentroidOk/Bad` |
| A/B structure | ratio | 1.0% | 3.0% | `Th.RatioOk/Bad` |
| A/B corner spread | ratio | 1.0% | 3.0% | `Th.RatioOk/Bad` |
| \|radial component\| | light | 2.0% | 5.0% | **inline literals** |
| centre clipping | expo | 0.05% | - | `Th.ExpoClip` |
| centre level | expo | 50-65% | - | **inline locals** |
| centre level (sky) | expo `--sky` | 15-50% | - | **inline locals** |

`Th.ExpoLo` / `Th.ExpoHi` (50/60) are **dead** - nothing references them, and they
contradict the values actually used (50/65). Don't trust them; fix or delete.

## PNG output

`SaveResidualPng` writes a diverging blue → white → red heat map, with white
pinned at **1.0** and the range scaled to the 1st/99th percentile plus 10%
padding. Pinning at 1.0 is valid for every caller: the structure residual is
`img/model ≈ 1`, the ratio is normalised to median 1, and the `--light` map is
`1 + radial/a`. If you add a caller whose data isn't centred on 1, the map will be
misleading.

The encoder writes signature + IHDR (8-bit, colour type 2 = RGB) + a single IDAT
(each row prefixed with filter byte 0, deflated via `ZLibStream`) + IEND, with a
hand-rolled CRC-32. **Verified**: output decodes correctly in a standard decoder.

Unlike the Python version, there is no 4-panel figure - only the residual map.
The console numbers are the same; the map exists so you can see the *shape* of a
problem (horizontal/vertical/diagonal gradient).

## Parity with Python

The C# port was written to be numerically identical to `flatcheck.py`, and the
README records that the algorithms were validated by re-implementing each formula
in Python (percentile interpolation, the Gauss solver vs `numpy.linalg.solve`,
normal equations vs `lstsq`, the PNG encoder). **Treat `flatcheck.py` as the
numeric oracle when changing a formula.**

Intentional divergences:

- No 4-panel matplotlib figure (residual PNG only).
- No NEF reading (no `rawpy`); Siril produces the FITS.
- No EXIF read, so no `suggest_shutter(...)` shutter-speed suggestions.
- `Percentile` on an empty list returns 0 instead of raising.

## Known issues and gotchas

Verified by running the current build unless marked otherwise. Issues 1 and 4 are
**pinned by tests** that assert the current wrong behaviour - Phase 8 of the refactor
has to flip those tests deliberately, so neither can be fixed by accident.

1. **Argument order is positional-only.** `args[0]` is *always* taken as the image
   path, so `flatcheck --expo file.fit` fails with
   `EROARE: Could not find file '...\--expo'`. Only `flatcheck file.fit --expo`
   works. `GetOpt` is a bare `Array.IndexOf`, with no validation that a flag's
   value exists or isn't another flag.
2. **Exit code is always 0 on success**, even for a `PROBLEMA` verdict.
   `ModeStructure` and `ModeRatio` return their verdict string and
   `FlatCheckCli.Run` *discards it*. Only an exception yields exit 1. This blocks
   any scripted / CI use.
3. **`ModeLight` can throw `NullReferenceException`** (code reading, not
   reproduced): if `Solve` returns `null` on the first sigma-clip iteration the
   loop `break`s with `coef` still null, and the next line dereferences `coef[0]`.
4. **FITS `NAXIS=1` gives a misleading error.** A 1-D HDU computes `npix = 0`, so
   `dataBlocks = 0` and `pos` never advances past the header; the reader then
   parses the data region as a header and eventually fails with
   `FITS: header incomplet.` instead of `niciun HDU cu imagine 2D gasit`.
   (Multi-HDU files with an empty `NAXIS=0` primary + IMAGE extension **do** work
   correctly - verified.)
5. **The header loop doesn't stop at `END`.** It sets `end = true` but keeps
   scanning the remaining cards in that 2880-block, so anything after `END` is
   still parsed into the dictionary.
6. **Card parsing is naive.** It splits the value at the first `/`, which
   corrupts any string value containing a slash (e.g. a path or date in a
   `COMMENT`-adjacent card). Fine for the numeric keys actually used.
7. **File extension is never validated** - `FitsReader.Load` is called on whatever path
   is given, and a non-FITS file surfaces as a parse error.
8. **Dead code**: `Stats.RobustPtp` is never called (`LargeScalePtp` inlines the
   same computation, but with p5/p95 rather than this function's p2/p98 default);
   `Th.ExpoLo`/`Th.ExpoHi` are never read; `RadialModel`'s `centers`/`prof` return
   values are discarded by `ModeStructure` (they fed the dropped Python profile
   panel). All three are slated for deletion in Phase 8.
9. **`CornerStats` can throw on tiny images.** With `h < 8`, `h/2 - bh` goes
   negative and indexing throws. Not reachable with real frames.
10. **`--sat 0`** makes `rng = 0` and every percentage becomes ±∞/NaN; unvalidated.
11. **`_crcTable` is lazily initialised static mutable state** - benign
    single-threaded, but a data race if anything is ever parallelised.
12. **Memory is heavy.** `ModeStructure` computes the median of the *full-resolution*
    raster just to print one line, and `Percentile` copies twice (`.ToList()` then
    `.ToArray()`). Measured peak ~330 MB on a 3000×2000 frame (~55 bytes/pixel);
    a 24 MP D3300 frame extrapolates to well over 1 GB. Runtime is fine
    (1.3 s structure / 0.6 s expo / 0.5 s light on 3000×2000).

## Build & test

```bash
dotnet build FlatCheck.sln -c Release
```

```bash
dotnet test FlatCheck.sln
```

The golden-output tests snapshot every mode's console text. When you change output
*on purpose*, re-approve them and read the resulting diff:

```bash
FLATCHECK_UPDATE_GOLDEN=1 dotnet test FlatCheck.sln
```

## Run

Self-contained binary:

```bash
dotnet publish -c Release -r win-x64 --self-contained
```

```bash
flatcheck process/pp_flat_stacked.fit                  # 1. structure
flatcheck pp_flat_A.fit --ratio pp_flat_B.fit          # 2. 180 rotation
flatcheck process/pp_light_00001.fit --light           # 3. end-to-end
flatcheck flat_brut.fit --expo                         # 4. exposure
flatcheck sky_flat.fit --expo --sky                    # 4b. sky flat
flatcheck flat_brut.fit --expo --sat 4095 --black 150  # manual scale (D3300 12-bit)
flatcheck flat.fit -o harta.png                        # custom PNG path
```

Remember: the flag must come *after* the file path.
