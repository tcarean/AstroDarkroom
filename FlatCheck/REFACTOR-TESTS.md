# Refactoring plan: making FlatCheck testable

> **Status: phases 0-4 are done** (96 tests, all green). Phases 5-8 are not started.
> What actually shipped, and where it deviated from the plan below, is recorded in
> "Execution notes" at the end.


Goal: unit tests for the numeric core, without changing a single output number.

The target architecture is the one already used by the sibling `CopyWcs` tool in
this repo, which solved the same problem (single-file FITS CLI → testable):
`Program.Main` is a one-line shim into a `Run(args, stdout, stderr)` orchestration
class, the engine is split into focused internal classes, and an xUnit project
references it via `InternalsVisibleTo`. Mirror that; don't invent a second style.

## Why the current shape resists testing

Three problems, in order of severity:

1. **Computation and presentation are fused.** Every mode interleaves
   `Console.WriteLine` with the arithmetic that produces the numbers. There is no
   way to assert on `radPct` other than scraping stdout and re-parsing a
   Romanian-formatted string.
2. **Everything is a private static in one class.** Nothing is reachable from a
   test assembly without reflection.
3. **I/O is path-based.** `LoadFits(string path)` and `WritePng(string path, ...)`
   force every test that touches the reader or the encoder to hit the filesystem.

Note that the numeric primitives themselves (`Percentile`, `Solve`, `Interp`,
`BlockMean`, `RadialModel`, `AzimuthalAsymmetry`, `LargeScalePtp`) are already
pure functions of their arguments. They only need to be made *reachable*. That is
why this plan is cheap: most of the work is moving code, not restructuring it.

## Guiding rule

**This is a refactor, not a rewrite.** The golden-output tests from Phase 0 must
keep passing byte-for-byte through Phases 1-6. Bug fixes come last (Phase 8), each
one paired with a test that pins it, and each one deliberately breaking a golden
file that you then re-approve.

`flatcheck.py` stays the numeric oracle. If a test and the C# disagree, check
Python before assuming the test is wrong.

## Target layout

```
FlatCheck/
  FlatCheck.sln
  FlatCheck/
    FlatCheck.csproj
    Program.cs              Main shim only
    FlatCheckCli.cs         Run(args, stdout, stderr) -> int; mode dispatch
    Options.cs              CLI parsing + validation
    Thresholds.cs           Th
    Img.cs                  Img, BlockMean, Prepare, Divide
    Stats.cs                Percentile, RobustPtp, Interp, Solve, Verdict, Rank
    FitsReader.cs           LoadFits(byte[]) / LoadFits(string), ReadSample, card parsing
    Metrics.cs              FitCenter, RadialModel, CornerStats,
                            AzimuthalAsymmetry, LargeScalePtp, TileBackground
    Analysis/
      StructureAnalyzer.cs  -> StructureResult
      RatioAnalyzer.cs      -> RatioResult
      LightAnalyzer.cs      -> LightResult
      ExposureAnalyzer.cs   -> ExposureResult
    Report/
      StructureReport.cs    StructureResult + TextWriter -> Romanian text
      RatioReport.cs
      LightReport.cs
      ExposureReport.cs
    PngWriter.cs            WritePng, WriteChunk, Crc32, Colormap, SaveResidualPng
  FlatCheck.Tests/
    FlatCheck.Tests.csproj
    FitsTestData.cs         synthetic FITS builders (see below)
    TempWorkspace.cs        disposable temp dir
    ...Tests.cs
```

The `Analysis` / `Report` split is the load-bearing change. Each analyzer is a
pure `Img -> record` function; each report is a pure `record -> text` function.
Tests assert on record fields (stable, typed) rather than on formatted strings.

Moving the app into a `FlatCheck/FlatCheck/` subfolder mirrors `WCS_Copy`'s
layout and the repo's earlier "reorganized folders" commit. Do it in Phase 1 while
the diff is still small.

## Test data strategy

> **Built, in `FlatCheck.Tests/FitsTestData.cs`.** The shipped API differs from the
> sketch below: `bzero` is nullable and defaults per BITPIX (32768 for 16-bit, 0
> otherwise); colour cubes have their own `BuildFitsCube`; and there are dedicated
> `BuildFitsWithEmptyPrimary` / `BuildFitsNaxis1` builders for the HDU-walking
> cases. Scene generators are `Radial`, `WithLinearGradient`, `WithBayerMosaic`,
> `WithOffset`, `Scaled`, `Constant` - a plain `Radial` doubles as the "debayered"
> case and `Scaled` as the "normalised" one. Read the file, not this section.

Do **not** commit real 24 MP FITS files. Build them in code.

`FitsTestData.cs` should expose a small builder producing an in-memory `byte[]`:

```csharp
internal static byte[] BuildFits(
    int width, int height,
    Func<int, int, double> pixel,       // value at (x, y)
    int bitpix = 16, double bzero = 32768, double bscale = 1,
    int naxis3 = 1, IEnumerable<(string, string)> extraCards = null);
```

plus named scene generators, because the assertions are only meaningful against a
known ground truth:

| generator | shape | expected |
|---|---|---|
| `PerfectRadial(k)` | `c·(1 - k·r_n²)` | all structure metrics ≈ 0, verdict OK |
| `WithLinearGradient(g)` | radial × `(1 + g·(x/w - 0.5))` | corner spread ≈ `g`·100%, azimuthal and residual fire, centroid offset non-zero |
| `WithBayerMosaic(rG, bG)` | radial × per-position gain | `--expo` sees CFA, picks the right brightest channel |
| `Debayered()` | radial, all 4 positions equal | `--expo` sanity gate fires (spread < 1.02) |
| `Normalised()` | radial scaled to ~0-1 | `--expo` sanity gate fires (level < 1%) |
| `Flat(value)` | constant | degenerate-input behaviour |

Keep generated frames small (256×256 to 1024×1024). At 1024×1024 the whole
pipeline runs in well under a second.

⚠ **But small frames hide code paths.** The golden tests use 512×512, at which
`Prepare`'s second `BlockMean` never runs. Whenever a test's coverage depends on
the frame *size* rather than its content, either pick a size that reaches the path
or test the function directly - and confirm by mutation, not by inspection.

**Reference values for the ground truth** should come from running `flatcheck.py`
on the same synthetic data, not from the C# itself - otherwise you are only
testing that the code still does what it does. Generate once, paste the numbers
into the tests as constants with a comment naming the Python invocation.

⚠ **Partly done.** Phase 4 *did* use the oracle: every expected value in
`StatsTests` was checked against numpy (`np.percentile`, `np.interp`,
`np.linalg.solve`) and matches exactly, so the primitives are independently
validated, not merely pinned.

The **golden files are not**. They were captured from the C# build itself, so they
prove the refactor changed nothing - checked separately by running the pre- and
post-refactor binaries over the same fixtures - but they do not confirm any
mode-level number is *correct*. That debt is still open, and Phase 6 is where it
comes due, since that is where tests start asserting a value is right rather than
merely unchanged.

## Phases

Each phase is independently committable and leaves the tool working.

### Phase 0 - safety net (no restructuring) ✅ DONE

The only production change: make `Main` reachable.

- `static int Main` → `internal static int Main`.
- Add `<InternalsVisibleTo Include="FlatCheck.Tests" />` to the csproj.
- Create `FlatCheck.Tests` (xUnit 2.5.3, Microsoft.NET.Test.Sdk 17.8.0,
  coverlet.collector 6.0.0 - matching the sibling's pinned versions).

Then write **golden-output characterization tests**: for each of ~8 scenarios
(clean structure, gradient structure, ratio clean/gradient, light, expo CFA, expo
sky, expo rejected-debayered, help text), redirect `Console.Out` via
`Console.SetOut`, call `Program.Main`, and compare against a committed `.txt`
golden file.

⚠ `Console.SetOut` is process-global, so these tests **cannot run in parallel**.
Put them in one `[Collection("console")]` until Phase 1 removes the need. This
constraint is itself a good argument for doing Phase 1 promptly.

*(As executed, this never applied: phases 0 and 1 were done as one step, so the
golden tests used a `StringWriter` from the start. Kept here for the reasoning.)*

Normalise the input path out of the golden files (the file path is echoed in
every header line) before comparing.

**Exit criterion:** golden tests pass against the unmodified algorithms. Nothing
after this point is allowed to change them until Phase 8.

### Phase 1 - `FlatCheckCli.Run(args, stdout, stderr)` ✅ DONE

Mechanical. Move the body of `Main` into `FlatCheckCli.Run`, thread a `TextWriter`
through every mode and every helper that prints, replace `Console.WriteLine` with
`stdout.WriteLine` and `Console.Error.WriteLine` with `stderr.WriteLine`.
`Program.Main` becomes:

```csharp
internal static int Main(string[] args)
    => FlatCheckCli.Run(args, Console.Out, Console.Error);
```

Rewrite the Phase 0 tests to pass a `StringWriter` instead of `Console.SetOut`;
drop the collection attribute. Also do the folder move here.

**Exit criterion:** golden files unchanged; tests run in parallel.

### Phase 2 - `Options` ✅ DONE

Extract CLI parsing into an `Options` record with a static `Parse(string[])`
returning either options or an error message. Cover: mode selection and
precedence, `-o`/`--out` default of `flatcheck.png`, `--sat`/`--black` invariant-culture
parsing, `-h`/`--help`.

Write `OptionsTests` **against current behaviour**, including a test that documents
the positional-only quirk (Known issue 1). Phase 8 flips that test.

### Phase 3 - `FitsReader` ✅ DONE

Extract with an in-memory entry point:

```csharp
internal static Img Load(byte[] raw);
internal static Img Load(string path) => Load(File.ReadAllBytes(path));
```

This is the single highest-value extraction - the reader is pure byte-shuffling
with a lot of edge cases and currently zero coverage.

`FitsReaderTests`:

- Round-trip each BITPIX: 8, 16, 32, -32, -64.
- `BZERO`/`BSCALE` applied as `bzero + bscale·stored` (the 16-bit + BZERO=32768
  unsigned convention is what Siril writes - test it explicitly).
- Big-endian byte order, including negative values for signed 16/32.
- Colour: `NAXIS3 = 3` plane-sequential averages across planes; `NAXIS3 = 1` and
  absent `NAXIS3` behave identically.
- Multi-HDU: empty `NAXIS=0` primary + IMAGE extension resolves to the extension
  (this **currently works** - lock it in).
- Header block padding: `END` at various card positions; headers spanning more
  than one 2880-block.
- Errors: unsupported BITPIX; truncated file; no 2-D HDU.
- `NAXIS=1` (Known issue 4) - assert the *current* wrong message, with a comment,
  then flip in Phase 8.

### Phase 4 - `Img` and `Stats` ✅ DONE

Pure moves. Tests with hand-computable answers.

Done. `Th` moved to `Thresholds.cs`, the raster and its transforms to `Img.cs`
(`BlockMean`, `Prepare`, `Divide` are now static members of `Img`), and the
primitives to `Stats.cs`. Tests: `PreprocessingTests`, `ImgTests`, `StatsTests`.

Covered early, during phases 0-3, because the golden suite could not see this code
(see Execution notes):

- ~~`BlockMean`: `k = 1` returns the input; `k = 2` on a known 4×4; partial-block
  truncation on odd dimensions (5×5 with `k = 2` → 2×2, last row/col dropped).~~
  Done, plus the 2×2-kills-CFA property.
- ~~`Prepare`: assert the `k` truncation quirk explicitly - 3000×2000 → 1500×1000,
  *not* 1200.~~ Done, plus the `k > 1` path (4800×128 → 1200×32) and an explicit
  `targetLong` override.

Covered in the phase-4 pass proper:

- `Percentile`: p0/p50/p100 on odd and even counts; the interpolation case
  (p25 of `[1,2,3,4]` = 1.75); single element; **empty list returns 0** (the
  documented divergence from numpy - pin it deliberately).
- `Interp`: clamping below `xs[0]` and above `xs[^1]`; exact-node hits; midpoint.
- `Solve`: identity; a known 3×3 system; a case requiring a pivot swap (zero in
  the first pivot position); a singular matrix returning `null`.
- `Divide`: the `Math.Max(b, 1e-12)` floor, including a negative denominator.
- `Verdict`/`Rank`: boundary values (exactly `ok`, exactly `bad`) and **`NaN` →
  PROBLEMA**.

### Phase 5 - `Metrics`

Pure moves; test against analytically-known scenes:

- `FitCenter`: on `PerfectRadial` returns the geometric centre within a pixel or
  two; on a shifted paraboloid returns the shift; on a constant image (singular)
  falls back to the geometric centre; on a fit whose vertex lands outside the
  frame, falls back.
- `RadialModel`: on `PerfectRadial` the residual `img/model` is ≈ 1 everywhere
  (assert p99 - p1 < 0.005); the profile is monotonically decreasing; bins with
  < 8 pixels are dropped.
- `CornerStats`: on `PerfectRadial` all four corners equal within tolerance; on
  `WithLinearGradient` left and right differ by the injected amount; centre box is
  brighter than the corners.
- `AzimuthalAsymmetry`: ≈ 0 on `PerfectRadial`; fires on a gradient; ring radii
  span `0.15·r_ins` to `r_ins`; returns empty for a degenerate tiny image.
- `LargeScalePtp`: returns `NaN` when fewer than 10 tiles land inside; averages
  down pure noise (feed uniform random noise, assert the result is far below the
  per-pixel spread - this is the property the metric exists for).
- `TileBackground`: the 15th percentile ignores injected bright "stars"; tile
  centres are at `(i + 0.5)·k`.

### Phase 6 - analyzers and reports

The real payoff. For each mode, split into `XxxAnalyzer.Analyze(...) -> XxxResult`
and `XxxReport.Write(result, TextWriter)`.

`StructureResult` should carry: analysis dimensions, raw median/max, fitted centre,
centroid offset %, per-corner values, corner spread, vignetting stops, azimuthal
max, residual ptp, the residual `Img`, per-test verdicts, overall verdict.
`LightResult`: `a..e` coefficients, `radPct`, `planePct`, kept/total tile counts,
verdict, the radial map. `ExposureResult`: white/black/source, the four CFA
medians, `isCfa`, brightest index, `cenMed`, `globPct`, `cenClip`, whether the
sanity gate fired and why, and the verdict enum. `RatioResult`: ptp, corner
spread, verdicts, the ratio `Img`.

Analyzer tests then assert on numbers directly:

- Structure on `PerfectRadial` → every verdict OK; on `WithLinearGradient(0.12)`
  → corner spread ≈ 11.7%, verdict PROBLEMA.
- Ratio of a frame against itself → ptp ≈ 0, spread ≈ 0, OK. Ratio of
  clean vs gradient → PROBLEMA. Mismatched dimensions → throws.
- Light: **the sign convention is the highest-value test in the suite.** A frame
  with residual vignetting (background falling toward edges) → `radPct > 0` and
  the under-correction message; an inverted one → `radPct < 0` and
  over-correction. Also: a pure linear gradient with no radial component →
  `planePct` large, `radPct` ≈ 0 (this is the "works under polluted sky" claim,
  and it is the thing most likely to silently regress). Also: sigma-clipping
  rejects injected bright tiles.
- Exposure: brightest-channel selection; the central window vs global median
  (assert `cenMed > globPct` on a vignetted frame - the whole rationale for the
  mode); each verdict band including boundaries at 30/50/65/80; both `--sky`
  bands; both sanity-gate triggers; `--sat`/`--black` overrides changing the
  percentage; the auto-detect ladder at each boundary (max = 1.0, 4300, 17203,
  above).

Report tests stay thin - one golden file per mode, plus a check that Romanian
text and numeric formatting (`F2`, `+0.00;-0.00`) are preserved. The Phase 0
golden tests migrate here.

### Phase 7 - `PngWriter`

Extract, and add a **decoder in the test assembly** - dependency-free and
cross-platform (do *not* use `System.Drawing`; it is Windows-only and obsolete
in .NET 8). Parse the chunk stream, verify each CRC, inflate the IDAT with
`ZLibStream`, and check dimensions, filter bytes, and pixel values.

- Signature bytes; IHDR fields (bit depth 8, colour type 2); IEND present and last.
- Every chunk CRC validates; CRC-32 against a known vector.
- A 2×2 image of known colours round-trips exactly.
- `Colormap`: `v == mid` → white (255,255,255); `v <= lo` → pure blue (0,0,255);
  `v >= hi` → pure red (255,0,0); clamping outside `[lo, hi]`.
- `SaveResidualPng` scaling: the 1/99 percentile range plus 10% padding, with
  white pinned at 1.0.

### Phase 8 - bug fixes, each pinned by a test

Only now, and one commit per fix, re-approving the golden files each time:

| # | fix | test |
|---|---|---|
| 1 | Accept flags before the path in `Options` | `flatcheck --expo f.fit` parses identically to `flatcheck f.fit --expo`; flag-as-value (`-o --light`) rejected |
| 2 | `Run` returns 2 for PROBLEMA, 1 for error, 0 for OK | exit code per verdict; **user-visible change - confirm the mapping first** |
| 3 | Guard `coef == null` in `LightAnalyzer` before dereferencing | singular input yields a clean error, not `NullReferenceException` |
| 4 | `NAXIS=1` → advance past the HDU, report "no 2-D image" | flip the Phase 3 test |
| 5 | `break` out of the card loop on `END` | a card after `END` is ignored |
| 6 | Validate `--sat > --black` | `--sat 0` → clean error, not NaN output |
| 7 | Delete `RobustPtp`, `Th.ExpoLo/ExpoHi`; move light's 2.0/5.0 and expo's bands into `Th` | thresholds referenced from one place; golden files unchanged |
| 8 | `Percentile` overload taking `double[]`/`Span` to drop the double copy; skip the full-res median or compute it without copying | allocation test, or just measure; behaviour unchanged |

Fixes 1 and 2 change the CLI contract. They are the ones worth having, since
together they make the tool scriptable - but check with the user before shipping
them, because any existing shell scripts depend on the current behaviour.

## Coverage targets

Not a number to chase, but by phase end the following should be fully covered:
`FitsReader` (every BITPIX and error path), `Stats` (every branch), `Metrics`
(every early-return and NaN path), and all four analyzers' verdict ladders
(every band, at both boundaries). The reports and `PngWriter` are adequately
covered by golden/round-trip tests.

## Effort

Phases 0-1 are perhaps a third of the work and deliver most of the safety;
if the effort has to be cut short, stop after Phase 3 (reader coverage) or
Phase 5 (metric coverage) - both are coherent stopping points that leave the
codebase better than a partial Phase 6.

## Commands

```bash
dotnet build FlatCheck.sln
```

```bash
dotnet test FlatCheck.sln
```


## Execution notes (phases 0-3)

Done, and green: 60 tests across `GoldenOutputTests` (13), `OptionsTests` (16),
`FitsReaderTests` (22) and `PreprocessingTests` (9).

**Behaviour is unchanged.** Verified by running the pre-refactor binary and the
post-refactor binary over the same fixtures: every printed number is identical,
across all four modes.

Three deviations from the plan as written:

1. **`Console.SetOut` was never needed.** Phase 0 and Phase 1 were done as one
   step - `FlatCheckCli.Run` landed immediately - so the golden tests took a
   `StringWriter` from the start. The serialized `[Collection("console")]`
   workaround the plan warned about never had to exist, and the suite is
   parallel-safe.

2. **The `TextWriter` parameter is named `outw`, not `w`.** Every mode already
   uses `w` for image width; `w` for the writer does not compile.

3. **`PreprocessingTests` was added, which the plan had deferred to Phase 4.**
   Reason: the golden suite was mutation-tested, and it *failed* the first
   mutation. Changing `Prepare`'s `targetLong` from 1200 to 600 left all 51
   golden tests green, because at the 512x512 frame size those tests use, the
   second `BlockMean` is a no-op (`k == 1` either way). The downsample arithmetic
   was completely uncovered.

   Fix: `BlockMean` and `Prepare` were made `internal` and tested directly,
   including the documented `k` truncation quirk (3000x2000 -> 1500x1000, *not*
   1200) and the `k > 1` path - exercised with a 4800x128 frame, which reaches
   `k == 2` without allocating a 24 MP raster. Re-running the same mutation now
   fails 2 tests.

   **Generalise this**: the golden tests only cover code paths the chosen frame
   size actually reaches. Before trusting them in later phases, mutate the thing
   you are about to refactor and confirm something goes red.

A second mutation (corrupting the 16-bit big-endian shift in `FitsReader`) fails
17 tests, so the reader coverage does have teeth.

**Not yet done, and still worth doing:** `Th`, `Img`, and the PNG encoder are all
still in `Core.cs` along with the four modes - that is Phases 4-7. The two
user-visible bug fixes (flag ordering, exit codes) remain deliberately unfixed and
are pinned by tests asserting the current wrong behaviour.


## Execution notes (phase 4)

`Th` -> `Thresholds.cs`, raster + transforms -> `Img.cs`, primitives -> `Stats.cs`.
`Core.cs` dropped from 745 to 635 lines and now holds only the metrics, the four
modes, and the PNG encoder. 96 tests green; the golden files did not change, and
the release binary prints identical numbers on the same fixtures.

**A correction to the phase 0-3 notes.** Those said `PreprocessingTests` would
"keep passing across that move untouched". It did not - moving `BlockMean` and
`Prepare` onto `Img` meant every call site in the test had to change from
`Core.X` to `Img.X`. The *assertions* were untouched, which is the part that
matters, but the claim as written was wrong.

**Placement decision:** `BlockMean`, `Prepare` and `Divide` became static members
of `Img` rather than a separate helper class, so the call sites read
`Img.Prepare(raw)`. `Divide` went from private to `internal` to be testable.

**Mutation-tested** (all caught): dropping `Percentile`'s linear interpolation
(5 tests), making `Solve` never report a singular matrix (1), turning `Verdict`'s
strict `<` into `<=` (2), and removing `Interp`'s upper clamp (1). The last two are
thin, but they are narrow behaviours with one correct answer each.

`Stats.RobustPtp` was moved rather than deleted, and carries a comment saying it is
dead and that its p2/p98 default disagrees with the p5/p95 `LargeScalePtp` inlines.
`StatsTests` pins both, so Phase 8 deleting it is a deliberate act - delete the test
alongside it.
