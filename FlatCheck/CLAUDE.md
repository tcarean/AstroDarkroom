# FlatCheck

Quantitative diagnostic for astrophotography flat frames. Answers four questions:
is the master flat radially symmetric, is the flat *setup* clean, does the flat
actually correct a real light, and is a raw flat exposed correctly.

Two applications - a console tool and a WPF window - over one core library, with
**exactly one NuGet dependency**: the core hand-parses FITS and hand-writes PNG (via
`ZLibStream` from the BCL), and leans on libraw (`Sdcb.LibRaw`) for camera raw files only.
.NET 8. See "The one dependency" below before adding a second, and "The two applications"
for what each one owns.

## Structure

Four projects. The split happened in 2.0.0, when a second application appeared over the
same numbers - see "The two applications" below for why it was a library extraction and
not a copy.

- `Directory.Build.props` - `<Version>` and `<Copyright>`, shared by every project. The
  **single** place to edit either on the C# side. See "Versioning".
- `FlatCheck.Core/` - everything that computes and everything that formats. Knows nothing
  about consoles or windows, and has no entry point, so it *cannot* reach `Console` even
  by accident. Before the split, "Analysis/ and Report/ are pure" was a convention held by
  goodwill; now it is a compile boundary.
  - `FitsReader.cs` - the hand-rolled FITS reader. `Load(byte[])` is the real
    entry point; `Load(string)` is a thin file wrapper, which is what lets the
    reader be tested without touching disk.
  - `IFrameReader.cs` - the contract each input format implements: `Handles(path)` and
    `Load(path) -> LoadedFrame`. The only interface in the codebase, and the reason
    `ImageLoader` knows no reader's API.
  - `ImageLoader.cs` - the **only** place that decides which reader gets a file. Holds one
    stateless instance per format and asks each `Handles`; what nobody claims falls back
    to FITS, and that fallback is an explicit field rather than the last array element, so
    a reorder cannot break it silently. `Load` returns a `LoadedFrame` (pixels, FITS
    header, ADU scale); the missing fields are the right answer per format, not errors - a
    raw has no FITS header, a FITS has no ADU scale. Nothing below it knows where the
    pixels came from, and that is what keeps the two formats from growing separate
    behaviour. Mirrors `load_image_with_header` in `flatcheck.py`.
  - `RawReader.cs` - the only file that touches a third-party library. Wraps libraw to
    get the undebayered Bayer mosaic plus the exact white/black levels out of a camera
    raw. `IsRawPath` is the extension gate; `RawExtensions` exposes the same list to the
    WPF app's `flats/` filter, so a camera the reader learns is a camera the list offers.
  - `Thresholds.cs` - `Th`. Note it does **not** hold every threshold; see below.
  - `Img.cs` - the 2-D raster plus its pure transforms: `BlockMean`, `Prepare`,
    `Divide`.
  - `Stats.cs` - numeric primitives: `Percentile`, `RobustPtp`, `Interp`, `Solve`,
    `Verdict`, `Rank`, `Severity`, and the verdict vocabulary (`ResidualBands`,
    `NotApplicable`).
  - `Metrics.cs` - the pure geometry and metrics: `FitCenter`, `RadialModel`,
    `CornerStats`, `AzimuthalAsymmetry`, `LargeScalePtp`, `TileBackground`.
  - `Version.cs` - `VersionInfo.Value`/`Banner`/`Copyright`, read from the assembly. See
    "Versioning".
  - `Analysis/` - one analyzer per mode, each a pure `Img -> XxxResult` function:
    `StructureAnalyzer`, `RatioAnalyzer`, `LightAnalyzer`, `ExposureAnalyzer`.
    No I/O, no printing - this is where every number is decided. Two of them are not
    `Img -> Result`, deliberately: `LightSuitability` is `header -> SuitabilityResult`
    (it decides whether `--light` applies at all, before any pixel is read), and
    `LightSeriesAnalyzer` is `[LightResult] -> LightSeriesResult` (it combines
    per-frame results; the file loading stays in `Runs/`, like every other mode).
  - `Report/` - one report per mode, each a pure `XxxResult + TextWriter -> text`
    function. All the Romanian output lives here and nowhere else. `LightSeriesReport`
    is split into `WriteHeader`/`WriteRow`/`WriteSummary` so a 32-frame run can stream
    its table instead of sitting silent for two minutes. `RunReport` holds the two lines
    that frame every mode - the version banner and the "harta salvata" line - because
    both applications write them and two copies would have diverged.
    Three pieces are exposed as values rather than only written: `StructureReport.Scale()`,
    `StructureReport.Advice()` and `LightReport.Diagnosis()`. The console formats them into
    columns, the WPF app puts them in a table and under a coloured chip; the wording has
    one definition either way.
  - `Runs/` - the whole chain of one mode: load -> analyze -> report -> encode the map.
    `StructureRun`, `RatioRun`, `ExposureRun`, `LightRun`, `LightSeriesRun`, plus
    `SeriesFinder` (which files a series run covers). This is the layer both applications
    call, and the reason neither of them re-derives a number. Two shape decisions are
    load-bearing and explained at length in `Runs/RunOutcome.cs`: the report is **written
    into a `TextWriter` the caller supplies** (so the console keeps streaming a 32-frame
    table, and anyone who wants the string passes a `StringWriter`), and the residual map
    comes back as **PNG bytes** rather than being written to disk (the window wants to
    show it, not save it).
  - `PngWriter.cs` - the hand-rolled PNG encoder and the residual colour map.
    `Encode(rgb, w, h)` is the real entry point; `EncodeResidualMap` is what both
    applications use, and `WriteResidualMap` is a thin file wrapper over it. It prints
    nothing - the "harta salvata" line comes from `RunReport`.
- `FlatCheck/` - the console app. `AssemblyName` is `flatcheck`, so the binary is
  `flatcheck.exe`, not `FlatCheck.exe`. Three files, and that is the point: what is left
  here is what belongs to a command line and to nothing else.
  - `Program.cs` - a one-line `Main` shim, plus a header that points at `README.md`
    rather than restating it. It used to carry a full copy of the user documentation;
    it drifted, and the copy was the wrong one - see the convention below.
  - `FlatCheckCli.cs` - `Run(args, stdout, stderr)`: mode dispatch, writing the map to
    disk, and the exit code. All output goes through the injected `TextWriter`, never
    `Console` directly.
  - `Options.cs` - CLI parsing.
- `FlatCheck.Wpf/` - the window app, `net8.0-windows`. See "The two applications".
  - `Session/` - `SirilSession` (what a session folder means on disk: which file is the
    master, which are ratio candidates, which are raw flats) and `SessionMessages` (the
    explanations shown when the input is not usable). The only part of the app with real
    rules, and the part with the most tests.
  - `ViewModels/` - one per tab over a shared `TabViewModel` (busy / result / error), plus
    `MainViewModel` for the folder. They are `public` because WPF binding reads public
    properties by reflection - and that has a useful consequence: a public view-model
    **cannot** expose an internal core type, so the compiler itself stops a
    `StructureResult` from leaking into XAML.
  - `Views/` - the XAML. `BusyOverlay`, `MessagePanel` and `ReportPanel` bind to the
    inherited `TabViewModel`, so the same three controls serve all four tabs.
  - `Presentation/` - `VerdictPalette` (colour derived from `Stats.Severity`, never from
    its own list of verdict names) and the value converters.
  - `Theme/Dark.xaml` - the palette, the control styles, the converters and the row
    templates. The converters live there rather than in `App.xaml` because a
    `StaticResource` inside a dictionary resolves in *that* dictionary.
  - `Interop/DarkTitleBar.cs` - the one P/Invoke: Windows draws the title bar, not WPF.
- `FlatCheck.Tests/` - xUnit tests for the core and the console, reaching them via
  `InternalsVisibleTo`. `Golden/` holds the characterization snapshots.
- `FlatCheck.Wpf.Tests/` - xUnit tests for the window app, `net8.0-windows`. Separate
  project rather than a folder, so `FlatCheck.Tests` - which carries the parity story -
  stays portable. Borrows `FitsTestData` and `TempWorkspace` from it by `<Compile Link>`
  rather than copying them.
- `flatcheck.py` - the original Python/numpy/astropy prototype the C# tool was
  ported from. Prior art only for *features*; **not maintained in parallel** - don't
  feel obliged to mirror C#-side changes into it, except where parity is tested (see
  "Parity with Python"), which now includes the `--light` guard and series mode. It is, however, the reference for
  *numeric* behaviour: see "Parity with Python" below.
- `README.md` - **the** user-facing documentation, in Romanian, and the single source
  for it. See the note in "Conventions".
- `LICENSE` - the verbatim GNU GPL v3 text. See "Licensing" below.

All eight phases of [REFACTOR-TESTS.md](REFACTOR-TESTS.md) are done.

## Conventions

- **User-facing text (CLI messages, `--help`, window labels, code comments) is in
  Romanian** - keep new text consistent with this. Documentation *about* the code
  (this file, the refactoring plan) is in English.
- **Nothing is computed outside `FlatCheck.Core`.** If a change needs a new number, a new
  threshold or a new sentence of diagnosis, it belongs in `Analysis/`, `Thresholds.cs` or
  `Report/` - never in a view-model. Both applications then get it, and get the same one.
  A view-model formats; it does not decide.
- **One external dependency, and it took a measured argument to earn it.** The FITS
  reader and PNG encoder stay hand-rolled. Don't add a NuGet package to replace them,
  and don't add a second one without the same standard of evidence; the point of this
  tool is that it is a single copyable binary. See "The one dependency".
- **Every metric is a ratio**, so ISO/gain cancels out. Don't add gain-dependent
  corrections - if a new metric isn't scale-invariant, that's a design smell.
- **Thresholds are empirical rules of thumb, not physical constants.** They are a
  starting point to be calibrated against a known-good setup.
- **Never bump the version and never commit** - both are the user's call. See
  "Versioning" and "Committing" below.
- **Every source file starts with the GPL notice.** New files included. See
  "Licensing" below.
- **User-facing documentation lives in `README.md`, and only there.** Do not restate the
  modes, the thresholds, the workflow traps or the `--light` validity limit in a
  docstring or a file header. It was duplicated between `README.md` and `flatcheck.py`'s
  docstring once, the two diverged, and the README was the wrong one - it still described
  a single-file `FlatCheck.cs`, a `csharp/` directory, three modes, and a port that had
  never been compiled. Code comments explain *why the code is the way it is*; the README
  explains *how to use the tool*.

## The one dependency

`Sdcb.LibRaw`, plus its `runtime.win64` / `runtime.linux64` native packages. It exists for
one reason: `--expo` on a camera raw, which was the last feature gap against
`flatcheck.py`.

**The alternatives were measured, not assumed.** There is no pure-managed .NET decoder for
a Nikon NEF - the pixels are Huffman-coded with a predictor and passed through a
linearization curve out of the MakerNote. What NuGet actually offers: `FC.SDK.Raw` is pure
managed but Canon-only; `NDCRaw` shells out to `dcraw.exe`; `HurlbertVisionLab.LibRawWrapper`
is C++/CLI and Windows-only; Vintasoft / Accusoft / LEADTOOLS / Atalasoft are commercial
*and* demosaic, which destroys the exact thing `--expo` measures; ImageSharp, SkiaSharp
and Magick.NET have no raw mosaic access at all. `Sdcb.LibRaw` binds the same libraw that
`rawpy` binds, so the numbers are bit-identical rather than close - verified on a real
D3300 NEF: same pixel sum (35598956557), same mean to twelve decimals, same four CFA
medians, same report text apart from the scale-source label.

Three properties keep the cost contained, and all three are worth preserving:

- **It is reached from exactly one file.** `RawReader.cs` is the only thing that names a
  `Sdcb.LibRaw` type. Everything downstream still sees an `Img`.
- **It is loaded lazily**, and that survives even deleting `Sdcb.LibRaw.dll` itself, not
  just the native DLLs: a FITS run exits 0, a raw run exits 1 with a message naming what
  is missing rather than letting a `0x8007007E` reach the user. `RawReader.Load` catches
  `DllNotFoundException` and friends for that.

  What makes it lazy is narrower than it looks, and was measured rather than assumed.
  `RawReader`'s **type** is loaded on every run, FITS included, because `ImageLoader` asks
  it `Handles`. Method bodies are only resolved at JIT time, so `Decode` costs nothing
  until a raw is actually opened - but **fields are resolved when the type is loaded**. A
  static field of a libraw *reference* type turns out to be harmless (the CLR defers), a
  field of a libraw *value* type breaks every FITS run. So: no libraw-typed fields on
  `RawReader`, of either kind. `HoldsNoLibrawTypedFields_SoTheAssemblyStaysLazilyLoaded`
  pins it; nothing else would notice.
- **Nothing is developed.** Only `raw_image` (cropped to the visible area, like rawpy's
  `raw_image_visible`), `color.maximum` and the black levels. No demosaic, no white
  balance, no gamma - all of which would make the measurement meaningless.

Don't add a second dependency at a lower standard of evidence than this one.

## The two applications

Since 2.0.0 there are two front ends over one core: `flatcheck.exe` (console) and
`FlatCheck.Wpf.exe` (window). **Neither reimplements a number.** Both call `Runs/`, which
loads the file, calls the analyzer, writes the report into a `TextWriter` and encodes the
map. What differs is only what each does with the three outputs.

| | console | window |
|---|---|---|
| input | one file (or a folder, with `--light`) | one Siril session folder |
| report | written to stdout | shown as panels, saved as `Rezultat testare.txt` |
| map | written to `-o <path>` | shown from memory, saved as `flatcheck.png` |
| result | exit code | coloured verdict |
| options | `--fov`, `--sat`, `--black`, `--pattern`, `--sky` | `--sky` only (a checkbox) |

The console keeps the options because it is the scriptable one, and because `--fov` and
`--sat` exist for telling the tool something the header does not say. Adding them to the
window would be four more controls in service of the rarer case.

### What the window adds, and what it does not

It adds a workflow: one folder instead of four invocations, `process/pp_flat_stacked.fit`
by convention instead of by typing, structure and end-to-end started on open. It does not
add an analysis, a threshold or a wording. If a change here needs a new number, that number
belongs in `Analysis/`, and the console gets it too.

**Structure and the light series run one after the other, not in parallel.** A 24 MP frame
runs into the high hundreds of MB in analysis (known issue 7) and the series holds a loaded
frame besides; two chains at once could pass a gigabyte on an ordinary machine. The time
saved would not be worth it.

### Three things about the WPF project that were learned the hard way

- **No `<InvariantGlobalization>`**, however tempting the symmetry with the console. WPF
  needs culture data to bind anything at all: without it the window throws
  `Cannot find non-neutral culture related to 'en-us'` while *loading* XAML - not on some
  path, on every start. The invariance that is actually needed is narrower and is done in
  code: `App.ForceInvariantNumbers` pins `DefaultThreadCurrentCulture`, so a Romanian
  Windows does not turn `0.24` into `0,24` in a file whose whole point is being identical
  to the console's output. `CultureTests` pins both halves.
- **The dispatcher is captured once per run, not asked per frame.** `LightSeriesRun`'s
  callback arrives on a pool thread and has to marshal onto the UI thread to touch an
  `ObservableCollection`. Reading `Application.Current` each time means the answer can
  change mid-series, half the rows land on one thread and half on another, and rows are
  lost silently - six frames analysed, five rows in the table. Measured, in a test run
  where the WPF `Application` appeared partway through.
- **`WindowSmokeTests` exists because the unit tests cannot see XAML.** A wrong resource
  key, a missing converter, a binding to a property that isn't there - all compile, and
  some fail at load. The suite renders every tab with real results into a
  `RenderTargetBitmap` and asserts that no binding error was traced. The
  `InvariantGlobalization` crash above passed 100 unit tests and never started; it would
  fail here. `FLATCHECK_DUMP_SCREENS=<dir>` also saves the renders as PNGs - useful for
  looking at the window without running it, and not a golden comparison: how a window
  looks is a job for eyes.

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
cancel itself out. `FitCenter` never supplies the centre the other metrics use - it
supplies the *tilt*, which is a test, and the vertex offset, which since 1.2.0 is only
informative (see "The residual scale").

## Input contract

Modes 1-3 want a **calibrated** FITS - flat-dark/offset already subtracted. In the
Siril workflow that is `process/pp_flat_stacked.fit` (the `pp_` prefix means
calibrated). An uncalibrated stack still has a ~150-600 ADU pedestal which
flattens the profile and under-reports vignetting by ~1%, and **all tests will
report OK anyway**, because the profile stays radially symmetric - only the
amplitude is wrong. Not detectable, only avoidable.

Mode 4 (`--expo`) is the exception: it wants a **raw, uncalibrated, single**
frame with the CFA mosaic intact and the original ADU scale, because stacking
normalises away the absolute level. The simplest way to satisfy that is to hand it the
NEF itself. Failing that, in Siril it means the **Conversion** tab (add NEFs → Convert),
*not* Open + Save, which debayers and normalises. The mode detects and rejects both
failure modes (see "Raw-file sanity gate").

Accepted formats: FITS (`.fit`/`.fits`/`.fts` - though the extension is never
actually checked, see Known issues), BITPIX 8/16/32/-32/-64, mono or colour; and
camera raw (`.nef`/`.cr2`/`.cr3`/`.arw`/`.dng`/`.raf`), where the extension **is**
what dispatches. All four modes accept both, exactly like `flatcheck.py`'s
`load_image_with_header`. A raw carries no FITS header, so `--light` sees an empty
dictionary and lands in the guard's `Unknown` layer - runs, with a note.

## The four modes

Dispatch order in `FlatCheckCli.Run` is `--expo` → `--ratio` → `--light` → structure
(default). The modes are mutually exclusive; the first match wins. The WPF app has no
dispatch: each tab is one mode.

Each mode's chain lives in `Runs/`, in the core, and both applications call it - see
"The two applications".

### 1. Structure (default) - `flatcheck flat.fit`

Is the master flat radially symmetric? Runs on the final master.

1. `FitsReader.Load` → `Prepare` (2×2 block-mean, then downsample toward ~1200 px).
2. `FitCenter` → one paraboloid fit on `log(I)`, two outputs. The **tilt** (the
   peak-to-peak amplitude of the fitted linear term, `2·sinh(|b|+|c|)`) is a test, on
   the shared residual scale. The **vertex offset** from the sensor centre, as a
   percentage of the half-diagonal, is printed informatively and carries no verdict -
   see "The residual scale" for why that changed in 1.2.0.
3. `CornerStats` → median of four 5% corner boxes and a central box. Reports
   vignetting in stops (informative only, *not* a test) and the spread between
   the four corners (a test - the corners should all be equal on a radial field).
4. `RadialModel` around the sensor centre → residual `img / model`, then
   `LargeScalePtp` of that residual (tile-averaged, inside the inscribed radius).
5. `AzimuthalAsymmetry` → per-ring peak-to-peak across 12 sectors; the max is the
   metric.
6. Verdict = the worst of the four tests - corner spread, azimuthal asymmetry,
   residual, tilt - on the shared residual scale (see
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

### 3. End-to-end - `flatcheck light.fit --light`, or `flatcheck process/ --light`

The only mode that closes the loop; 1 and 2 are proxies. Input is a light frame
or stack **calibrated with the flat but with no background extraction**, or a
**directory**, which runs the whole series (see "Series mode" below).

**But it does not apply to every dataset, and the mode now refuses rather than
guessing.** Read "The validity limit" below before touching anything here - it is the
single most important thing about this mode, and it was missing until a real session
made the tool report a confident false positive.

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

#### The validity limit

The plane/radial split works **only while the light-pollution dome is quasi-linear
across the frame**. Over a wide field the dome *curves inside the frame*, and the
radially symmetric part of that curvature has exactly the shape of vignetting. Two
things with the same shape cannot be separated by a fit, however well written. This is
a geometric degeneracy, not an implementation limit - do not try to fix it with a
better fit.

It was measured, not assumed. On `Milkyway-Core/2026-08-12-Smida` (Tokina 20 mm f/2.8
on a D3300, ~70° diagonal, Milky Way core low in the sky), a single frame reported
`+3.13 % SUSPECT, under-correction`. Across all 32 calibrated subs of that session the
same figure swept smoothly from `+3.13 %` through `-19.20 %` to `+35.73 %` in 81
minutes, with only 1.36 points between consecutive frames. Correlation between the
reported radial component and the sky gradient: **-0.94**, R² = 0.88. Eighty-eight
percent of what the tool called "flat" was sky.

`LightSuitability.Check` therefore gates the mode, deriving the diagonal field of view
in layers, most trusted first:

| source | when |
|---|---|
| `--fov <deg>` | stated by the user; beats everything |
| WCS `CDELT1` / `CD1_1` | a plate solve is present; measured, not deduced |
| `XPIXSZ` + `FOCALLEN` (+ binning) | astro cameras; the exact field |
| `FOCALLEN` alone | DSLRs - Siril does **not** write `XPIXSZ` for them |
| nothing | **runs anyway**, with a note |

Levels: `< Th.LightFovOk` (20°) `Ok`, up to `Th.LightFovMax` (40°) `Marginal` (runs, with
a warning), above that `TooWide` - **refused**, exit 1, no figure produced at all.

Two decisions here are load-bearing and easy to break:

- **Unknown is not a refusal.** If a missing header meant "too wide", every synthetic
  fixture in the test suite - and every minimal FITS - would become unanalysable, and the
  guard would do more harm than the problem it fixes. Pinned by
  `AnEmptyHeaderRunsInsteadOfRefusing`.
- **The focal-only fallback reports no field figure.** The field depends on the sensor,
  which is unknown at that point, so printing a number would be inventing one. It grades
  the focal length directly against thresholds computed for the largest plausible sensor
  (full frame, 43.27 mm diagonal), which is conservative: on a smaller sensor a good frame
  can come out `Marginal`. That is what `--fov` is for. Pinned by
  `TheFocalOnlyFallbackIsGradedOnTheFocalLength`.

`--fov` is not a force flag. It tells the tool the truth; it does not ask it to ignore
the physics, and a wide field declared by hand is refused exactly like a deduced one.

#### Series mode - `flatcheck process/ --light`

A single frame's `radPct` is the sum of two things: the flat error, which is **the same
in every frame by construction** (the same master flat was divided out of each), and a
sky radial term, which changes through the session. So the series carries a discriminator
the single frame does not.

`LightSeriesRun.Execute` loads each matching file in turn - never more than one full
frame in memory, since a 24 MP frame runs to hundreds of MB - runs the unchanged
`LightAnalyzer.Analyze` on it, streams a table row, and hands the results to
`LightSeriesAnalyzer.Combine`, which is pure.

Of the five runs it is the only one that had anything worth duplicating - a loop, with
state, progress and a memory trick that is not visible from the signature. It therefore
has two outputs rather than one: rows go into the caller's `TextWriter` as frames finish
(so the console does not sit silent for two minutes), and the same frame goes out through
an optional `onFrame` callback (so the window fills its table live). Cancellation is
checked *between* frames; a frame already started runs to the end, which at ~0.5 s each
is a fine response time.

The verdict is given **only if all three conditions hold**:

| condition | threshold |
|---|---|
| frame count | ≥ `Th.LightSeriesMinFrames` (5) |
| standard error of the mean (`σ/√n`, ddof=1) | < `Th.LightSeriesSemMax` (2.0) |
| \|Pearson(radPct, planePct)\| | < `Th.LightSeriesSkyCorr` (0.70) |

Otherwise the verdict is **`NEDETERMINAT`** and the report says which condition failed
and why. Three details that are not obvious:

- The correlation test is **skipped** unless the sky gradient actually varies across the
  series (`Th.LightSeriesSkySpanMin`, 10 points). On a clean session where it sits
  constant, the correlation is noise and would reject good data.
- The comparisons are written `!(x < threshold)`, not `(x >= threshold)`, so **NaN falls
  on the bad side** - same convention as `Stats.Verdict`. A one-frame series has no
  spread, `sem` is NaN, and it must not pass silently.
- `planePct` being large is **informative only**, never a gate. The plane is an indirect
  proxy - the real cause of non-separation is curvature - and a badly calibrated threshold
  there would reject good data. The field-of-view guard covers the cause.

**What was tried and rejected:** extrapolating to zero sky gradient. Regressing
`radPct ~ planePct` over the real session gives an intercept of `+122 %` - an
extrapolation far outside the data, whose gradient never goes near zero (it spans 49-88 %).
The correlation is a good diagnostic and a useless estimator; do not turn it into one.

**File selection.** `SeriesFinder.Find` enumerates the directory and matches the pattern
**in-process**, rather than `Directory.GetFiles(dir, pattern)`: Win32 matching still
carries the 8.3 legacy where `*.fit` also catches `.fits`. Sorting is ordinal on the file
name (`StringComparer.Ordinal` / Python `sorted()`) - if the two implementations listed
frames in different orders, `crosscheck.py` would report differences that do not exist.
The glob supports `*` and `?` only; character classes are literal in both, so `fnmatch`
is deliberately not used on the Python side. The WPF app reuses the same matcher for its
`process/` listings, so `pp_flat*.fit` there means exactly what `--pattern` means here.

The default pattern `pp_light_*.fit` excludes `r_pp_light_*` because the glob is anchored
at the start, and that is correct rather than incidental: registered frames are
geometrically transformed, so the sensor-fixed flat error is shifted relative to the
centre the radial model is fitted around. Including them would corrupt the measurement
silently.

Thresholds are all in `Th` (`LightRadOk`/`LightRadBad`, the `LightFov*`/`LightFocal*`
guard, and the `LightSeries*` family).

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

**On a camera raw nothing is guessed.** The file carries both levels, and `RawReader`
hands them to the analyzer as an `ExposureScale`, which is reported as
`sursa: libraw (exact)`. Precedence is Python's, and the order matters: `--sat` beats the
file *and* takes over the source line (the reported scale is no longer the file's), while
`--black` is overridden separately - so a bare `--sat` leaves the file's black in place.
No file scale means the `AutoWhite` ladder, unchanged. Pinned by
`AScaleReadFromTheFileBeatsTheGuess` and the two override tests beside it.

White comes from libraw's `color.maximum` and black from `color.black + color.cblack[i]`,
median of the four - which is what `rawpy.black_level_per_channel` reports. The split
between the two fields is libraw's business and it varies: on Nikon NEFs it moves the
common part into `black` and leaves `cblack` at zero, so reading only one of them would
silently give zero on some cameras. On a D3300 at 12 bits that comes out 4095 / 150.

**Raw-file sanity gate.** Before issuing a verdict, the mode rejects the file if
either `cenMed < 1.0` (level near zero → already calibrated or normalised) or the
CFA spread `< 1.02` (the four Bayer positions are near-identical → the file is
debayered). This catches the common "opened the NEF in Siril and hit Save"
mistake. When it fires, the mode prints an explanation and returns **without a
verdict**.

Verdict bands on the central window (`Th.Expo*`):

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
and returns `(cx, cy, tiltPct, vertexValid)`.

`tiltPct = 2·sinh(|b| + |c|)·100` is the peak-to-peak amplitude of the **linear** term
over the frame: `|b| + |c|` is its value at the corner (where `xn = yn = 1`), and `sinh`
converts exactly from log space to intensity instead of the linear approximation
`2(|b| + |c|)`. It is a fraction, so it is scale-invariant like every other metric, and
it recovers an injected gradient to within ~1%. The cross term `f` (astigmatism/saddle)
is deliberately left out: it is not a tilt, and the residual test catches it anyway.

The vertex comes from `M = [[2d, f], [f, 2e]]`. It falls back to the geometric centre -
with `vertexValid = false`, so the report can say `nedeterminat` instead of printing a
fake `0.00` - if the system is singular or the vertex lands outside the frame. If the
normal equations themselves are singular the tilt is `NaN`, which degrades to `REFA`
like every other NaN in the tool.

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

Every threshold lives in `Th`. Full picture:

| metric | mode | OK < | PROBLEMA ≥ | defined in |
|---|---|---|---|---|
| corner spread | structure | *residual scale* | | `Th.ResidPerfect/Usable/Check` |
| azimuthal asymmetry (max) | structure | *residual scale* | | `Th.ResidPerfect/Usable/Check` |
| residual after radial model | structure | *residual scale* | | `Th.ResidPerfect/Usable/Check` |
| tilt (linear term) | structure | *residual scale* | | `Th.ResidPerfect/Usable/Check` |
| A/B structure | ratio | 1.0% | 3.0% | `Th.RatioOk/Bad` |
| A/B corner spread | ratio | 1.0% | 3.0% | `Th.RatioOk/Bad` |
| \|radial component\| | light | 2.0% | 5.0% | `Th.LightRadOk/Bad` |
| field of view (diagonal) | light | 20° | 40° = refused | `Th.LightFovOk/Max` |
| focal length, no pixel size | light | 120 mm | below 60 mm = refused | `Th.LightFocalOk/Min` |
| standard error of the mean | light, series | 2.0 pts | | `Th.LightSeriesSemMax` |
| \|corr(radPct, planePct)\| | light, series | 0.70 | | `Th.LightSeriesSkyCorr` |
| frame count | light, series | ≥ 5 | | `Th.LightSeriesMinFrames` |
| centre clipping | expo | 0.05% | - | `Th.ExpoClip` |
| centre level | expo | 50-65% | - | `Th.ExpoLo/Hi` |
| centre level (sky) | expo `--sky` | 15-50% | - | `Th.ExpoSkyLo/Hi` |

### The residual scale

The four structure deviation tests - corner spread, azimuthal asymmetry, residual after
the radial model, and tilt - all measure *the same thing* from different angles: how much
of the frame fails to be explained by a purely radial function. All four are fractional
amplitudes in percent, so they share one four-band scale rather than a pass/fail
threshold:

| band | verdict | meaning |
|---|---|---|
| < 2% | `PERFECT` | quality is perfect |
| 2-10% | `BUN` | usable; background extraction removes what is left |
| 10-20% | `ACCEPTABIL` | usable, but run `--light` to confirm before a hard stretch - if your field is narrow enough for it, see "The validity limit" |
| > 20% | `REFA` | reshoot the flats |

Boundaries are strict `<`, so each one falls into the worse band. `NaN` degrades to
`REFA`, same rule as `Verdict`.

**`reziduu dupa model rad.` is the one that matters most**, and the console output says
so inline. It measures the departure from the radial model directly - which *is* the
flat error. The other three are partial views of the same deviation: the corners catch it
only where the four boxes sit, the azimuthal test only inside the inscribed circle, and
the tilt only the part of it that is a plane.

The structure mode prints the scale itself, under the heading
`SCARA celor patru teste de abatere de mai sus`, so the numbers can be read without
consulting these docs.

The illumination-centre offset is **not** on this scale, and since 1.2.0 it is not a test
at all: it prints under `INFORMATIV (nu sunt teste)` with no verdict. It measures a
position, which is the wrong shape for this tool. The vertex is `-b/(2d)`, a
gradient-over-curvature ratio - not a fraction, and not scale-invariant. Measured, with
the same 11% gradient held fixed: 2.38% at 2 stops of vignetting, 9.49% at 0.5, 31.16% at
0.15, 57.40% at 0.08 - and then **0.00%** at 0.05, because the vertex leaves the frame and
the out-of-frame guard returns the sensor centre. So the metric was not merely
miscalibrated, it was **non-monotonic**: past that point the worse the defect, the cleaner
the report, which is a silent false negative on exactly the flattest lenses. The fixtures
never caught it because `FitsTestData.Radial` defaults to `k = 0.45`, i.e. 0.86 stops -
5.7x deeper than a real Samyang 135 at f/2.8.

`inclinare` is that same defect taken as an amplitude, which is what the scale wants, and
it still catches genuine decentring: a radial field centred off the sensor centre produces
a linear term of about `offset x curvature`, so the tilt weights the offset by what it
actually costs photometrically. On the real Samyang master that prompted the change (0.15
stops, an 11% twilight-sky gradient) the offset said 42.36% `PROBLEMA` and the mode said
`REFA`; the tilt says 12.36% `ACCEPTABIL`, in line with the other three tests
(12.62 / 7.05 / 6.05). Pinned by `FitCenter_TiltIsInvariantToTheVignettingDepth`,
`FitCenter_WhenAFlatFieldMakesTheVertexEscape_TheTiltStillReadsTheGradient` and
`OnAFlatLensWithAGradient_TheTiltCarriesTheVerdict`.

`Stats.Severity` still spans both ladders (`OK`/`PERFECT` = 0, `BUN` = 1,
`SUSPECT`/`ACCEPTABIL` = 2, `PROBLEMA`/`REFA` = 3): `--ratio` and `--light` keep the
`OK`/`SUSPECT`/`PROBLEMA` vocabulary, and the exit code is derived across all modes.

The `--ratio` mode keeps its own tighter 1%/3% thresholds. Its job is different: the A/B
ratio should be *featureless*, and "background extraction removes it" does not apply to
a setup-validation test.

Since phase 8 every threshold lives in `Th` - `--light` no longer has 2.0/5.0 inline
and `--expo` no longer keeps its bands as locals. `Th.ExpoLo`/`Th.ExpoHi` used to be
dead *and wrong* (50/60 while the code used 50/65); they now hold the real values.
`Th.ExpoTarget` (57) and `Th.ExpoSkyTarget` (30) are the midpoints used for the
suggested-stops arithmetic.

## PNG output

`PngWriter.EncodeResidualMap` produces a diverging blue → white → red heat map, with white
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

**Current status: 38 of 39 scenarios agree numerically** - 22 byte-identical, 16
differing only in wording. Coverage spans all four modes, all four bands of the residual
scale, both gradient directions, non-square frames, BITPIX 16/32/-32, and the exposure
bands either side of each boundary.

Intentional divergences:

- No 4-panel matplotlib figure (residual PNG only).
- No EXIF read beyond the pixels, so no `suggest_shutter(...)` shutter-speed
  suggestions - libraw hands over `shutter`, but nothing consumes it yet.
- `Percentile` on an empty list returns 0 instead of raising.
- The scale-source line reads `libraw (exact)` where Python reads `rawpy (exact)`. Same
  library; `rawpy` is its Python wrapper.
- `--sat` is echoed as `--sat=4095` rather than Python's `--sat=4095.0`.
- The `--light` interpretation capitalises the diagnosis - "Fondul CADE/CRESTE spre
  margini" against Python's lower case. The sign *is* the diagnosis, so the emphasis
  is deliberate. This accounts for the remaining wording-only differences in the four
  `--light` scenarios whose radial component is large enough to print a diagnosis.

**One numeric divergence, and the C# is the correct one.** On a *perfectly constant*
frame `FitCenter`'s quadratic coefficients are pure floating-point noise (~1e-15) and
the vertex determinant comes out at ~1.3e-29. C# guards with `|det| < 1e-15`, marks the
vertex unusable and prints `nedeterminat`. Python has no such guard: `np.linalg.solve`
does not raise on a matrix that is only *numerically* singular, so it returns garbage and
places the illumination peak at (136, 136) on a 256x256 raster, reporting a 6.35% offset.

Since 1.2.0 the difference is confined to the informative line: the offset no longer
votes, so both implementations reach the same verdict and Python's false positive changes
nothing. It used to be the difference between OK and SUSPECT.

This cannot occur on a real flat, which always has vignetting and therefore a
determinant far from zero. Do not "fix" the C# to match; the guard is what makes the
degenerate case behave. Pinned by
`FitCenter_OnAConstantImage_FallsBackToTheGeometricCentre`.

## Exit codes

Since phase 8 the tool is usable from a script:

| code | meaning |
|---|---|
| 0 | OK / PERFECT / BUN - usable as-is |
| 1 | error - missing file, bad argument, invalid FITS, failed fit, `--expo` on a file that isn't a raw flat, or `--light` on a field too wide for the test |
| 2 | SUSPECT / ACCEPTABIL / NEDETERMINAT (and `--expo`'s two "acceptable but..." bands) |
| 3 | PROBLEMA / REFA |

Verdict severity is monotonic, so "anything non-zero" means "not clean, go look".

## Known issues and gotchas

Verified by running the current build unless marked otherwise. The CLI-contract and
correctness bugs that used to live here were fixed in phase 8; what remains is
behaviour that is either deliberate or not worth changing.

1. **Card parsing is naive.** It splits the value at the first `/`, which corrupts any
   string value containing a slash (e.g. a path or date in a `COMMENT`-adjacent card).
   Fine for the numeric keys actually used.
2. **File extension is only half-validated.** The six raw extensions dispatch to
   `RawReader`; *everything else* goes to `FitsReader.Load`, so a `.jpg` surfaces as a
   FITS parse error rather than "unrecognised format". Python raises
   `Format nerecunoscut` instead. Left as is deliberately: the error still points at the
   right file, and tightening it would change behaviour no test asks for.
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

Both applications print it from `RunReport.WriteBanner`, so the WPF app's saved
`Rezultat testare.txt` opens on the same two lines as a console run, and a bump moves both
at once.

The version lives in **two** places:

| where | what |
|---|---|
| `Directory.Build.props` | `<Version>` - the single source of truth for C# |
| `flatcheck.py` | `__version__` |

`Directory.Build.props`, not a csproj, and that changed in 2.0.0 for a concrete reason:
there are now three assemblies. `VersionInfo` reads the one it lives in - Core - but the
console exe and the WPF exe carry their own, which is what Windows shows in file
properties. Two places to edit means, sooner or later, two different values. So the
property sits above all of them and every project inherits it.
**Don't move `<Version>` or `<Copyright>` back into a csproj**; `VersionTests` checks that
no project overrides either.

`FlatCheck.Core/Version.cs` does **not** hardcode it. `VersionInfo.Value` reads the
assembly's `AssemblyInformationalVersion`, which MSBuild derives from `<Version>`, and
trims any `+sourcerevision` suffix. Don't reintroduce a literal there.

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
| `<Version>` removed from `Directory.Build.props`, or re-added to a csproj | the plumbing tests in `VersionTests` |
| the banner stops being printed | the golden files, because they carry a placeholder line the output no longer produces |

The scrubbing does not weaken coverage: `VersionTests` asserts the banner's exact content
and its position on line 1, for every mode, for `--help`, and for failed runs.

## Licensing

The tool is **GPLv3-or-later**. The full text is in `LICENSE` at the repo root (the
verbatim FSF `gpl-3.0.txt` - don't reword it, and don't reflow it).

Four things follow, and all four are pinned by tests:

- **Every source file starts with the notice**, above the file's own header comment:
  the SPDX line, the copyright, and all three paragraphs of the standard GNU notice
  (permission, no-warranty, where-to-get-a-copy). `//` for C#, `#` for Python (below the
  shebang), and for XAML a single `<!-- ... -->` block wrapping the unprefixed text -
  `--` cannot repeat inside an XML comment. XAML counts as source: it is written by hand
  and it describes the interface. New files get the notice too - it is the first thing to
  write, not an afterthought. `LicenseHeaderTests` walks all four projects plus the two
  `.py` files and fails on any file missing it, which is the only thing keeping the
  convention alive as files get added; its `Projects` list is explicit rather than
  discovered, so adding a project is a decision. The SPDX line is there so licence
  scanners can read the file without parsing English.
- **Every run prints the copyright on line 2**, right under the version banner, in every
  mode, at `--help`, and on failed runs. Same reason as the banner: a pasted report says
  on its own whose tool produced it.
- **`--help` carries the warranty disclaimer** and points at `LICENSE` - GPLv3 §5(d),
  which wants a program that displays legal notices to display that one too. It lives
  there rather than under the banner on purpose: five lines on top of every report,
  every run, would drown the numbers. `WriteUsage` in C#, the argparse `epilog`
  (`__warranty__`) in Python.
- **One source of truth per language for the copyright string**: `<Copyright>` in
  `Directory.Build.props` (read back through `AssemblyCopyrightAttribute` by
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

**The solution no longer builds on Linux**, because `FlatCheck.Wpf` targets
`net8.0-windows`. That is a deliberate cost, not an oversight: the console stays portable
if you build its project directly, and there is no CI workflow in the repo, so nothing
else notices.

```bash
dotnet build FlatCheck/FlatCheck.csproj -c Release
```

```bash
dotnet test FlatCheck.sln
```

Two test projects run: `FlatCheck.Tests` (core + console, portable) and
`FlatCheck.Wpf.Tests` (window, Windows-only).

The golden-output tests snapshot every mode's console text. When you change output
*on purpose*, re-approve them and read the resulting diff:

```bash
FLATCHECK_UPDATE_GOLDEN=1 dotnet test FlatCheck.sln
```

To look at the window without running it - handy when the Windows session is locked and a
screen capture would catch the lock screen:

```bash
FLATCHECK_DUMP_SCREENS=/tmp/ecrane dotnet test FlatCheck.Wpf.Tests --filter WindowSmokeTests
```

## Run

Self-contained binary:

```bash
dotnet publish FlatCheck/FlatCheck.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

The last two flags are what keep the "single copyable binary" promise now that libraw is
in: without them its four native DLLs sit next to the exe as separate files. With them
the whole thing is one ~67 MB `flatcheck.exe` again. `linux-x64` works the same; other
RIDs (arm64, macOS) have no libraw binary in these packages, so FITS-only there.

Note the explicit project path: `dotnet publish` on the solution would now also try the
WPF project, which cannot be single-filed the same way.

The window app publishes without `PublishSingleFile`:

```bash
dotnet publish FlatCheck.Wpf/FlatCheck.Wpf.csproj -c Release -r win-x64 --self-contained
```

```bash
FlatCheck.Wpf.exe "D:\Astro\2026-08-12-Smida"   # optional: the session folder
```

```bash
flatcheck process/pp_flat_stacked.fit                  # 1. structure
flatcheck pp_flat_A.fit --ratio pp_flat_B.fit          # 2. 180 rotation
flatcheck process/pp_light_00001.fit --light           # 3. end-to-end
flatcheck flat_brut.fit --expo                         # 4. exposure
flatcheck DSC_0060.NEF --expo                          # 4. exposure, straight off the raw
flatcheck sky_flat.fit --expo --sky                    # 4b. sky flat
flatcheck flat_brut.fit --expo --sat 4095 --black 150  # override, mostly for FITS
flatcheck flat.fit -o harta.png                        # custom PNG path
```

Remember: the flag must come *after* the file path.
