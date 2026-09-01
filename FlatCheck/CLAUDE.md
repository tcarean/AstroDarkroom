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
  - `Version.cs` - `VersionInfo.Value`/`Banner`, read from the assembly. See "Versioning".
  - `FitsReader.cs` - the hand-rolled FITS reader. `Load(byte[])` is the real
    entry point; `Load(string)` is a thin file wrapper, which is what lets the
    reader be tested without touching disk.
  - `Thresholds.cs` - `Th`. Note it does **not** hold every threshold; see below.
  - `Img.cs` - the 2-D raster plus its pure transforms: `BlockMean`, `Prepare`,
    `Divide`.
  - `Stats.cs` - numeric primitives: `Percentile`, `RobustPtp`, `Interp`, `Solve`,
    `Verdict`, `Rank`.
  - `Metrics.cs` - the pure geometry and metrics: `FitCenter`, `RadialModel`,
    `CornerStats`, `AzimuthalAsymmetry`, `LargeScalePtp`, `TileBackground`.
  - `Analysis/` - one analyzer per mode, each a pure `Img -> XxxResult` function:
    `StructureAnalyzer`, `RatioAnalyzer`, `LightAnalyzer`, `ExposureAnalyzer`.
    No I/O, no printing - this is where every number is decided.
  - `Report/` - one report per mode, each a pure `XxxResult + TextWriter -> text`
    function. All the Romanian output lives here and nowhere else.
  - `PngWriter.cs` - the hand-rolled PNG encoder and the residual colour map.
    `Encode(rgb, w, h)` is the real entry point; `Write`/`WriteResidualMap` are thin
    file wrappers, which is what lets the encoder be tested without touching disk.
    It prints nothing - the "harta salvata" line is written by `FlatCheckCli`.
- `FlatCheck.Tests/` - xUnit tests, reaching the app via `InternalsVisibleTo`.
  `Golden/` holds the characterization snapshots.
- `flatcheck.py` - the original Python/numpy/astropy prototype the C# tool was
  ported from. Prior art only; **not maintained in parallel** - don't feel
  obliged to mirror C#-side changes into it. It is, however, the reference for
  *numeric* behaviour: see "Parity with Python" below.
- `README.md` - user-facing docs, in Romanian.
- `LICENSE` - the verbatim GNU GPL v3 text. See "Licensing" below.

All eight phases of [REFACTOR-TESTS.md](REFACTOR-TESTS.md) are done.

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
- **Never bump the version and never commit** - both are the user's call. See
  "Versioning" and "Committing" below.
- **Every source file starts with the GPL notice.** New files included. See
  "Licensing" below.

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
6. Verdict = the worst of the four tests, on the shared residual scale (see
   "The residual scale" below). Prints that scale, and marks
   `reziduu dupa model rad.` as the metric that matters most. Writes the residual
   heat map PNG.

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
| corner spread | structure | *residual scale* | | `Th.ResidPerfect/Usable/Check` |
| azimuthal asymmetry (max) | structure | *residual scale* | | `Th.ResidPerfect/Usable/Check` |
| residual after radial model | structure | *residual scale* | | `Th.ResidPerfect/Usable/Check` |
| illumination-centre offset | structure | 4.0% | 10.0% | `Th.CentroidOk/Bad` |
| A/B structure | ratio | 1.0% | 3.0% | `Th.RatioOk/Bad` |
| A/B corner spread | ratio | 1.0% | 3.0% | `Th.RatioOk/Bad` |
| \|radial component\| | light | 2.0% | 5.0% | `Th.LightRadOk/Bad` |
| centre clipping | expo | 0.05% | - | `Th.ExpoClip` |
| centre level | expo | 50-65% | - | `Th.ExpoLo/Hi` |
| centre level (sky) | expo `--sky` | 15-50% | - | `Th.ExpoSkyLo/Hi` |

### The residual scale

The three structure deviation tests - corner spread, azimuthal asymmetry, and residual
after the radial model - all measure *the same thing* from different angles: how much of
the frame fails to be explained by a purely radial function. So they share one four-band
scale rather than a pass/fail threshold:

| band | verdict | meaning |
|---|---|---|
| < 2% | `PERFECT` | quality is perfect |
| 2-10% | `BUN` | usable; background extraction removes what is left |
| 10-20% | `ACCEPTABIL` | usable, but run `--light` to confirm before a hard stretch |
| > 20% | `REFA` | reshoot the flats |

Boundaries are strict `<`, so each one falls into the worse band. `NaN` degrades to
`REFA`, same rule as `Verdict`.

**`reziduu dupa model rad.` is the one that matters most**, and the console output says
so inline. It measures the departure from the radial model directly - which *is* the
flat error. The corner spread and the azimuthal asymmetry are partial views of the same
deviation: the corners catch it only where the four boxes sit, the azimuthal test only
inside the inscribed circle.

The structure mode prints the scale itself, under the heading
`SCARA celor trei teste de abatere de mai sus`, so the numbers can be read without
consulting these docs.

The illumination-centre offset is **not** on this scale - it measures a position, not an
amplitude - and keeps `OK`/`SUSPECT`/`PROBLEMA`. `Stats.Severity` spans both ladders
(`OK`/`PERFECT` = 0, `BUN` = 1, `SUSPECT`/`ACCEPTABIL` = 2, `PROBLEMA`/`REFA` = 3) so the
overall verdict can still be "the worst of the four"; it is reported in the residual
vocabulary.

The `--ratio` mode keeps its own tighter 1%/3% thresholds. Its job is different: the A/B
ratio should be *featureless*, and "background extraction removes it" does not apply to
a setup-validation test.

Since phase 8 every threshold lives in `Th` - `--light` no longer has 2.0/5.0 inline
and `--expo` no longer keeps its bands as locals. `Th.ExpoLo`/`Th.ExpoHi` used to be
dead *and wrong* (50/60 while the code used 50/65); they now hold the real values.
`Th.ExpoTarget` (57) and `Th.ExpoSkyTarget` (30) are the midpoints used for the
suggested-stops arithmetic.

## PNG output

`SaveResidualPng` writes a diverging blue → white → red heat map, with white
pinned at **1.0** and the range scaled to the 1st/99th percentile plus 10%
padding. Pinning at 1.0 is valid for every caller: the structure residual is
`img/model ≈ 1`, the ratio is normalised to median 1, and the `--light` map is
`1 + radial/a`. If you add a caller whose data isn't centred on 1, the map will be
misleading.

The encoder writes signature + IHDR (8-bit, colour type 2 = RGB) + a single IDAT
(each row prefixed with filter byte 0, deflated via `ZLibStream`) + IEND, with a
hand-rolled CRC-32.

**Verified three ways**: `PngWriterTests` decodes the output with an independent
reader written in the test assembly (`PngDecoder` - deliberately not
`System.Drawing`, which is Windows-only and obsolete on .NET 8), checks every chunk
CRC, and pins CRC-32 against the standard `"123456789"` -> `0xCBF43926` vector and
the well-known empty-IEND constant `0xAE426082`. The bytes were also confirmed to
decode in a real platform decoder, and are byte-identical to what the tool produced
before the refactor began.

Unlike the Python version, there is no 4-panel figure - only the residual map.
The console numbers are the same; the map exists so you can see the *shape* of a
problem (horizontal/vertical/diagonal gradient).

## Parity with Python

The C# port was written to be numerically identical to `flatcheck.py`. **Treat
`flatcheck.py` as the numeric oracle when changing a formula**, and re-run the
cross-check afterwards:

```bash
FLATCHECK_DUMP_FIXTURES=/tmp/xc dotnet test FlatCheck.sln --filter DumpsFixtures
```

```bash
python crosscheck.py /tmp/xc
```

`crosscheck.py` runs both implementations over identical FITS across 34 scenarios and
compares twice: the full text, and every numeric token as a float. It also checks once,
up front, that both report the same version. It exits non-zero on any unexplained numeric
difference or on a version desync.

**Current status: 33 of 34 scenarios agree numerically** - 20 byte-identical, 13
differing only in wording. Coverage spans all four modes, all four bands of the residual
scale, both gradient directions, non-square frames, BITPIX 16/32/-32, and the exposure
bands either side of each boundary.

Intentional divergences:

- No 4-panel matplotlib figure (residual PNG only).
- No NEF reading (no `rawpy`); Siril produces the FITS. The `--expo` rejection message
  differs accordingly - Python offers "run it on the NEF directly", which C# cannot.
- No EXIF read, so no `suggest_shutter(...)` shutter-speed suggestions.
- `Percentile` on an empty list returns 0 instead of raising.
- `--sat` is echoed as `--sat=4095` rather than Python's `--sat=4095.0`.
- The `--light` interpretation capitalises the diagnosis - "Fondul CADE/CRESTE spre
  margini" against Python's lower case. The sign *is* the diagnosis, so the emphasis
  is deliberate. This accounts for the remaining wording-only differences in the four
  `--light` scenarios whose radial component is large enough to print a diagnosis.

**One numeric divergence, and the C# is the correct one.** On a *perfectly constant*
frame `FitCenter`'s quadratic coefficients are pure floating-point noise (~1e-15) and
the vertex determinant comes out at ~1.3e-29. C# guards with `|det| < 1e-15` and falls
back to the sensor centre, reporting a 0.00% offset and OK. Python has no such guard:
`np.linalg.solve` does not raise on a matrix that is only *numerically* singular, so it
returns garbage, places the illumination peak at (136, 136) on a 256x256 raster, and
reports a 6.35% offset with a SUSPECT verdict - a false positive on a uniform frame.

This cannot occur on a real flat, which always has vignetting and therefore a
determinant far from zero. Do not "fix" the C# to match; the guard is what makes the
degenerate case behave. Pinned by
`FitCenter_OnAConstantImage_FallsBackToTheGeometricCentre`.

## Exit codes

Since phase 8 the tool is usable from a script:

| code | meaning |
|---|---|
| 0 | OK / PERFECT / BUN - usable as-is |
| 1 | error - missing file, bad argument, invalid FITS, failed fit, or `--expo` on a file that isn't a raw flat |
| 2 | SUSPECT / ACCEPTABIL (and `--expo`'s two "acceptable but..." bands) |
| 3 | PROBLEMA / REFA |

Verdict severity is monotonic, so "anything non-zero" means "not clean, go look".

## Known issues and gotchas

Verified by running the current build unless marked otherwise. The CLI-contract and
correctness bugs that used to live here were fixed in phase 8; what remains is
behaviour that is either deliberate or not worth changing.

1. **Card parsing is naive.** It splits the value at the first `/`, which corrupts any
   string value containing a slash (e.g. a path or date in a `COMMENT`-adjacent card).
   Fine for the numeric keys actually used.
2. **File extension is never validated** - `FitsReader.Load` is called on whatever path
   is given, and a non-FITS file surfaces as a parse error.
3. **`CornerStats` can throw on tiny images.** With `h < 8`, `h/2 - bh` goes negative
   and indexing throws. Not reachable with real frames.
4. **`_crcTable` is lazily initialised static mutable state** - benign single-threaded,
   but a data race if anything is ever parallelised.
5. **The corner-spread metric has a non-zero floor.** The analysis centre is
   `(w/2, h/2)`, but pixel indices run `0..w-1`, whose true middle is `(w-1)/2`. That
   half-pixel offset puts the top-left corner fractionally farther from the centre than
   the bottom-right, so even a *perfectly* radial field reports a spread. It scales as
   ~1/n: measured 2.88% at 100 px, 1.44% at 200, 0.56% at 512, 0.28% at 1024
   (cross-checked against a numpy reimplementation). At the ~1500x1000 analysis raster
   the tool actually uses it is ~0.2%, far below the 2% PERFECT band, so it is
   harmless in practice - but it is a systematic floor, not noise, and it would swamp
   the threshold on a small enough frame. Pinned by
   `CornerStats_HasAHalfPixelCentringArtifact_ThatHalvesAsTheFrameDoubles`.
   **Not a bug to fix**: changing the centre convention would break parity with
   `flatcheck.py`.
6. **`RadialModel`'s `centers`/`prof` return values are discarded** by
   `StructureAnalyzer` - they fed the Python profile panel, which this port dropped.
7. **Memory is still heavy**, though phase 8 halved the worst of it: `Percentile` now
   has a `double[]` overload, so taking the full-resolution median no longer copies
   twice. Measured peak was ~330 MB on a 3000x2000 frame before that change; a 24 MP
   D3300 frame still runs into the high hundreds of MB. Runtime is fine
   (1.3 s structure / 0.6 s expo / 0.5 s light on 3000x2000).

## Versioning

Every run prints `flatcheck <version>` as its **first line** - every mode, `--help`, and
failed runs too, so a pasted report always says which version produced it. There is no
`--version` flag; it would be redundant.

The version lives in **two** places:

| where | what |
|---|---|
| `FlatCheck/FlatCheck.csproj` | `<Version>` - the single source of truth for C# |
| `flatcheck.py` | `__version__` |

`FlatCheck/Version.cs` does **not** hardcode it. `VersionInfo.Value` reads the assembly's
`AssemblyInformationalVersion`, which MSBuild derives from `<Version>`, and trims any
`+sourcerevision` suffix. So on the C# side there is exactly one place to edit. Don't
reintroduce a literal there.

**The user bumps the version manually**, per feature or fix, choosing the increment
themselves. **Never bump it on your own initiative** - not as part of a change, not
"while you're in there". If a change seems to warrant a bump, say so and let them decide.

**Bumping the version breaks nothing.** That is deliberate, and worth preserving: the
banner is scrubbed to `flatcheck <VERSIUNE>` before any comparison, in both places that
compare whole output.

- `GoldenOutputTests.RunCli` scrubs it, the same way it already scrubs absolute paths -
  both change without the *behaviour* changing. So `Golden/*.txt` carry the placeholder
  and survive a bump untouched.
- `crosscheck.py` scrubs it in `norm()`, and checks version agreement once up front in
  `check_versions()`. Without that, a desync would turn all 34 scenarios into diffs -
  34 diffs to learn one thing.

What *does* fail, and should:

| situation | what fails |
|---|---|
| both places bumped | nothing |
| only one place bumped | exactly one test, `PythonReportsTheSameVersionAsTheCSharp`; `crosscheck.py` prints one `[VERSIUNE]` line and exits 1 |
| `<Version>` removed from csproj | the format and plumbing tests in `VersionTests` |
| the banner stops being printed | the golden files, because they carry a placeholder line the output no longer produces |

The scrubbing does not weaken coverage: `VersionTests` asserts the banner's exact content
and its position on line 1, for every mode, for `--help`, and for failed runs.

## Licensing

The tool is **GPLv3-or-later**. The full text is in `LICENSE` at the repo root (the
verbatim FSF `gpl-3.0.txt` - don't reword it, and don't reflow it).

Four things follow, and all four are pinned by tests:

- **Every source file starts with the notice**, above the file's own header comment:
  the SPDX line, the copyright, and all three paragraphs of the standard GNU notice
  (permission, no-warranty, where-to-get-a-copy). `//` for C#, `#` for Python, below
  the shebang. New files get it too - it is the first thing to write, not an
  afterthought. `LicenseHeaderTests` walks both projects plus the two `.py` files and
  fails on any file missing it, which is the only thing keeping the convention alive
  as files get added. The SPDX line is there so licence scanners can read the file
  without parsing English.
- **Every run prints the copyright on line 2**, right under the version banner, in every
  mode, at `--help`, and on failed runs. Same reason as the banner: a pasted report says
  on its own whose tool produced it.
- **`--help` carries the warranty disclaimer** and points at `LICENSE` - GPLv3 §5(d),
  which wants a program that displays legal notices to display that one too. It lives
  there rather than under the banner on purpose: five lines on top of every report,
  every run, would drown the numbers. `WriteUsage` in C#, the argparse `epilog`
  (`__warranty__`) in Python.
- **One source of truth per language for the copyright string**: `<Copyright>` in
  `FlatCheck/FlatCheck.csproj` (read back through `AssemblyCopyrightAttribute` by
  `VersionInfo.Copyright` - the literal in `ReadCopyright` is only a fallback), and
  `__copyright__` in `flatcheck.py`. `VersionTests` binds all three together.

Unlike the version banner, the copyright line is **not** scrubbed anywhere: it is
constant, so the golden files carry it verbatim and `crosscheck.py` compares it as
ordinary text. That is deliberate - if the line ever stops being printed, the golden
files fail. `--help` is not one of `crosscheck.py`'s 34 scenarios (argparse and
`WriteUsage` produce entirely different text), so the two warranty notices are worded
the same by choice, not by test.

The year and the author are the user's call, exactly like the version. Don't roll the
year forward on your own initiative.

## Committing

**Do not commit.** Make the changes, run the tests, and hand over a commit message for
the user to use. They commit themselves.

This also means: don't `git add`, don't stage, don't amend. Leave the working tree as it
is so they can review the diff before deciding.

A good commit message here follows the existing log: a short lowercase subject line, then
a body that explains *why* rather than restating the diff - what the change is for, what
was deliberately left alone and why, and anything a future reader would otherwise have to
rediscover. No `Co-Authored-By` trailer.

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
