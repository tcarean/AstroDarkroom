# Speed up the print-background simulation with one linear lift table, no figure changes

Branch `claude/Speed-up-the-print-simulation-with-one-lift-table`, worktree
`.claude/worktrees/Speed-up-the-print-simulation-with-one-lift-table` (the kit's rule "Worktrees").
Saved copy at the end of the task. The figures measured while executing it are in ADR 0035 and
`docs/validation.md` (History, 2026-10-09); one step was added during the task: a grid row's two
pixel shares come from `TonalAnalyzer.LstarShares`, without the median's sort and the histogram
the row does not show.

## Context

`PrintStretchSimulator.Simulate` lifts the stride-4 subsample once per step of the ladder
(`ExactTargetLo`..`ExactTargetHi` every `ExactTargetStep`, 56 steps, plus the 16 grid rows and the
explicit target, most of which coincide). Today each step does two full passes over the ~4.2 M
samples of the subsample (3 planes of 5837 x 3867 / 16 on the plate-solved FITS):

1. `RasterOps.Gamma(sub, gamma)` allocates a lifted raster and fills it through a 65 537-entry
   `Math.Pow` table with linear interpolation between levels (cheap per sample, but 16 MB per step).
2. `DustAnalyzer.PatchLstars(lifted)` linearises every lifted sample again with
   `ColorMath.SrgbToLinear`, a `Math.Pow(x, 2.4)` per sample: ~250 M `Pow` per run, the part that
   costs. For the grid rows `TonalAnalyzer.LstarStats(lifted, dmax, 1)` adds a third pass with
   three more `Pow` and a `Cbrt` per pixel.

The code review of ADR 0025/0026 found it: V-004 went from 11 to 23 s and the window sits on
"praf" longer than on everything else. The owner's proposal: per step, one table on the 65 536
16-bit levels of `lin(pow(k / 65535, gamma))`, from which the patch luminances are summed directly,
no intermediate raster; the same table serves the L\* statistics of the grid rows. Measure first,
then implement. Constraints: no behaviour change (so no requirement; an ADR for the design choice
plus `docs/design/measurements.md`); nothing computed outside Core; every figure of
`docs/validation.json` identical within tolerance (`/validate` before and after, drift table
pasted); goldens unchanged; the time on `starless_rbf0.5_stretch0.17.fit` before and after written
in `docs/validation.md`.

### What changes numerically, and why it is within tolerance

Today a sample `s` becomes `lifted = (float)interp(PowLut, s)` and then `SrgbToLinear(lifted)`.
With the table it becomes `interp(LinLut, s)` where `LinLut[k] = SrgbToLinear(Pow(k / 65535, gamma))`.
At an exact 16-bit level the two agree to the float rounding of the old intermediate raster
(~1e-8 relative); between levels the difference is the second-order error of interpolating the
sRGB curve over one 16-bit step, below 1e-10 in luminance, i.e. below 1e-7 L\* on a patch mean. The
printed figures carry one decimal (L\*) or three (targets). The as-is row is not touched: it keeps the
exact `SrgbToLinear` path, so the map, the as-is dust and sky and the curves' anchors are bitwise
the same. Step 1 measures the actual maximum difference on the reference image before the code
is written, as the owner asked.

Assumption stated (the kit's rule "Two readings"): the as-is measurement stays on the exact path;
only the lifted steps go through the table. Lifting the as-is row through a table at gamma 1
would be interpolation for nothing and could move figures the window shows bitwise today.

## Step 0: worktree and data

- `git worktree add .claude/worktrees/Speed-up-the-print-simulation-with-one-lift-table -b claude/Speed-up-the-print-simulation-with-one-lift-table main`
- Copy from the main checkout's `AstroPhotoAnalyzer.Tests/testdata/large/` into the worktree's:
  `de_printat.png`, `starless_rbf0.5_stretch0.17.fit`, `Rosetta_starless_no_WCS.fit`,
  `Orion_with_WCS.fit`, `Hahnemuhle PRBaryta.icm`, `HARMAN_Canvas.icc` (what `validation.json` names;
  ADR 0004). Run `scripts/check-testdata.ps1`.

## Step 1: measure before (nothing committed from this step)

1. `/validate` on the untouched branch: paste the `MEASURED` lines; this is the "before" column of
   the drift table.
2. Time the console on the FITS, three runs, the median kept:
   `Measure-Command { dotnet AstroPhotoAnalyzer/bin/Release/net8.0/AstroPhotoAnalyzer.dll <fits> --dmax 2.0 }`
   (Release build first). Also a scratch xunit test (in the scratchpad or a temporary file deleted
   before commit) that times `PrintStretchSimulator.Simulate` alone on the FITS with a `Stopwatch`,
   so the stage figure is separate from reading and the rest.
3. The numeric probe, the measurement the owner asked for before implementing: in the same scratch
   test, for every ladder gamma of the FITS, compute the patch L\*s both ways (today's
   `Gamma` + `PatchLstars`, and the table) and print the maximum |ΔL\*| over all patches and all
   steps, plus the same on `de_printat.png` (a 16-bit source, where levels are exact). Expected under
   1e-5 L\*; if it is not, stop and report before writing the code.

## Step 2: code (all in `AstroPhotoAnalyzer.Core`)

- **New `Input/LiftTable.cs`**: the table and the two linearisers.
  - `internal interface ISrgbLinearizer { double Linear(float s); }`
  - `internal readonly struct ExactLinearizer : ISrgbLinearizer` — `ColorMath.SrgbToLinear(s)`,
    today's path, unchanged (NaN is not expected here; the readers clip it, R-013).
  - `internal readonly struct LiftTable : ISrgbLinearizer` — built for one gamma:
    `double[65537]` of `SrgbToLinear(Math.Pow(k / 65535.0, gamma))`; `Linear(s)`: NaN -> 0, clamp to
    [0, 1], `v = s * 65535.0`, `k = (int)v`, `f = v - k`, `f == 0 ? lut[k] : lut[k] + f * (lut[k+1] - lut[k])`
    (the same indexing as `RasterOps.Gamma`, so a NaN or a 1.0 cannot index past the end).
  - Generic struct constraint (`where T : struct, ISrgbLinearizer`) so the JIT inlines the call: no
    delegate per sample, one loop body for both paths instead of two copies. Romanian header comment
    saying why a table, why interpolated on the linear value, and the measured error bound.
- **`Input/RasterOps.cs`**: factor the `Pow` table of `Gamma` into `internal static double[] PowerLut(double gamma)`
  so `LiftTable` and `Gamma` build it the same way (`Gamma`'s output does not change: `PrintPreview`,
  the window's map and `RasterOpsTests` keep it). `LstarPlane(Raster r, int stride)` keeps its
  signature and calls a new `LstarPlane<T>(Raster s, T lin)` on the subsample with `ExactLinearizer`;
  the generic one does `LstarOfY(Luminance(lin.Linear(R), lin.Linear(G), lin.Linear(B)))` per pixel
  (mono: `LstarOfY(lin.Linear(M))`).
- **`Analysis/DustAnalyzer.cs`**: `PatchLstars(Raster sub)` keeps its signature (`CurveAdvisor`, the
  tests, the as-is row) and calls `PatchLstars<T>(Raster sub, T lin)`; the summation order per patch
  stays exactly as today (double sum of per-sample luminance, divided by `side * side`).
- **`Analysis/TonalAnalyzer.cs`**: `LstarStats(Raster r, double dmax, int stride)` keeps its signature
  and delegates to a new overload `LstarStats(float[] L, double dmax)` holding today's loop (median,
  the three shares, the histogram).
- **`Analysis/PrintStretchSimulator.cs`**: per ladder step, `var lift = new LiftTable(gamma)`;
  `DustAnalyzer.Measure(DustAnalyzer.PatchLstars(sub, lift), map, black)`; for a table row
  `TonalAnalyzer.LstarStats(RasterOps.LstarPlane(sub, lift), p.Dmax)`. No `RasterOps.Gamma`, no
  lifted raster. Update the header comment's last paragraph (it describes the 16-bit table of the
  lift) and the inline comment about `stride: 1`.
- Nothing in the console or the window changes.

## Step 3: tests (`AstroPhotoAnalyzer.Tests`, new `LiftTableTests.cs`; no requirement ID, no behaviour change)

- At every 16-bit level `k / 65535f`, `LiftTable.Linear` equals `SrgbToLinear(Math.Pow(level, gamma))`
  within 1e-12 for a few gammas (0.5, 0.78, 1, 2).
- On 10 000 seeded random floats, |table − exact| below the bound measured in step 1 (write the
  measured bound, not a guess).
- NaN -> 0 and 1f -> `SrgbToLinear(1)` (the table's end).
- `PatchLstars(sub, LiftTable)` against `PatchLstars(RasterOps.Gamma(sub, gamma))` on a random
  float raster: every patch within 1e-5 L\*; on a raster quantised to 16-bit levels, within 1e-6.
- `LstarPlane(sub, LiftTable)` against `LstarPlane(RasterOps.Gamma(sub, gamma), 1)` likewise, and
  the `LstarStats(float[])` overload returning the same tuple as the raster one.
- The existing `PrintStretchSimulatorTests`, `DustSimulationTests`, `GoldenOutputTests` must pass
  untouched; `git status` must show no change under `AstroPhotoAnalyzer.Tests/Golden/`.

## Step 4: measure after

- `/validate`: paste the `MEASURED` lines; the drift table before/after per case (expected: no figure
  outside tolerance; report the largest absolute change seen, even inside tolerance).
- The same three timed console runs and the scratch stage timer as in step 1; delete the scratch test.
- `scripts/verify.ps1` green (format, build, tests, design-note check).

## Step 5: docs

- **ADR 0035** `docs/decisions/0035-each-lift-is-linearised-through-one-table-not-a-lifted-raster.md`:
  context (the review finding, the 250 M `Pow`, 11 -> 23 s), decision (per step one table of
  `lin(pow(level, gamma))`, interpolated on the linear value, summed straight into the patches; the
  as-is row stays exact), alternatives rejected (bisection instead of a ladder: the ladder serves 116
  cloud rows and the Dmax neighbours from the same lifts, ADR 0025; caching `Pow` per distinct float:
  a float FITS has millions of distinct values; a table on the lifted float: a second interpolation for
  nothing), consequences (the measured ΔL\* bound, the time before/after, the tests that guard it).
- **`docs/design/measurements.md`**: B2's "through `RasterOps.LstarPlane`" and B4's
  "(`RasterOps.Gamma`, a 16-bit lookup table ...)" rewritten to name `LiftTable` and the two
  linearisers; **`docs/design/input.md`**: a paragraph on `LiftTable` and its file in the `Covers:`
  sentence (the gate's design check); **`docs/design/code-map.md`**: the new file's row.
- **`docs/validation.md`**: a dated History line: no figure moved (or the largest change inside
  tolerance), the console time on the FITS before -> after and the simulation stage before -> after,
  machine and build noted; **`docs/skills.md`** `/validate` step 1: "11 to 23 s" -> the new range.
- **`docs/requirements.md`** R-013: the sentence that names `RasterOps.Gamma` as the table a NaN would
  break also names `LiftTable` (same behaviour, same reason; no status change).
- **`CHANGELOG.md`** under `[Unreleased]` / `Changed`: the background simulation is N x faster
  (the measured figure), every figure unchanged (ADR 0035).

## Step 6: wrap-up

`/wrap-up`: verify green, read the whole diff back, the plan saved to `docs/plans/`, commit on the
branch (one line, English, <= 100 chars), propose the merge and wait for the owner.

## Verification

- `scripts/verify.ps1` green; `git diff --stat main -- AstroPhotoAnalyzer.Tests/Golden/` empty.
- `/validate` before and after with identical verdicts and every figure within tolerance; the drift
  table pasted in the final message.
- Timing table (console run and simulation stage, FITS) before and after in the final message and
  in `docs/validation.md`.
- `dotnet run --project AstroPhotoAnalyzer.Wpf -- <fits>` not required; the window calls the same
  `AnalysisRun.Execute`.
