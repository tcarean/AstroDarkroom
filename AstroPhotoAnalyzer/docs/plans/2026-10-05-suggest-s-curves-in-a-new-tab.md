# Suggest S-curves in a new tab (R-140 to R-147, ADR 0031)

## Context

The owner wants the window to suggest contrast curves for a finished image, show a preview of
each, show the curve as a table and a chart that can be typed into Siril's Curves tool, and
save a new FITS with the curve applied, the input never touched. The measurements of
2026-10-05 on the three reference images settled the design (ADR 0031, written, accepted in
chat): five anchors (dust and sky from Tonuri; band, bright clouds and peaks from the upper
part of their classes, or from the frame's histogram without a WCS), the slopes of the owner's
earlier curve, three strengths plus `Maxima`, a natural cubic spline per channel as Siril
computes it, no lift of the sky (that stays the re-stretch), the gamut as a figure only, always
a FITS on save.

Already on the branch `claude/Suggest-S-curves-in-a-new-tab` (worktree
`.claude/worktrees/Suggest-S-curves-in-a-new-tab`), uncommitted: the eight requirement rows
(`draft`), ADR 0031, the acceptance tests `AstroPhotoAnalyzer.Tests/CurveTests.cs` and
`AstroPhotoAnalyzer.Wpf.Tests/CurvesTabTests.cs` (42 compile errors, all for types that do not
exist yet), and the unmeasured case V-008. The tests fix the API named below.

## Step 0: one measurement before any code (gamut, R-144)

`IccPaperBlack` uses Wacton.Unicolour device -> PCS only. Its README documents the other
direction (`.Icc`, B2A tables) with a CMYK example. In a scratch console project outside the
repo: load `Hahnemuhle PRBaryta.icm` and `HARMAN_Canvas.icc`, round-trip sRGB -> device -> Lab
for a 17 x 17 x 17 grid, time it, and compare the out-of-gamut share of the 0.17 FITS with the
LittleCMS figures of the study (4.2 % baryta, 8.6 % canvas; without black point compensation
the darks will differ, so the comparison is per class above L\* 10).

- Works and agrees: R-144 is built on a round-trip table (grid once per profile, trilinear
  lookup per pixel), no new package.
- Does not work for RGB printer profiles, or disagrees: stop and ask the owner (drop R-144 to a
  later task, or a package; the kit's rule "Packages"). The rest of the plan does not depend on
  it.

## Step 1: core, numbers (`AstroPhotoAnalyzer.Core/`)

- `Thresholds.cs`: `CurveBandClassPercentile` 80, `CurveCloudClassPercentile` 75,
  `CurveBandPercentile` 94.2, `CurveCloudPercentile` 99.1, `CurvePeakPercentile` 99.9,
  `CurveSlopeSkyBand` 1.70, `CurveSlopeBandCloud` 1.48, `CurveSlopeCloudPeak` 0.94,
  `CurvePeakTarget` 0.92, `CurveStrengths` {0.5, 0.75, 1}, `CurveMaximaMinGain` 0.02,
  `CurveClipLevel` 0.995, `CurveShadowChartFactor` 1.25, `CurveChartSamples`; each with the
  comment saying it came from the owner's earlier curve or the study of 2026-10-05.
- `Analysis/ToneCurve.cs` (new): `CurvePoint(X, Y, Label)`; `ToneCurve` validates the points
  (throws `ArgumentException`), solves the natural spline by the tridiagonal recurrence Siril
  uses, `Map`, `Slope`, `IsMonotone` (on a 65 536-entry table, after clamping), `Apply(Raster)`
  through that table with linear interpolation between entries, into a new raster.
- `Analysis/CurveAdvisor.cs` (new): anchors (`CurveAnchors`, `CurveAnchorSource`) from
  `SimulationResult` (`AsIs.LstarDust/LstarSky`, `Map.Classes`, `AsIsDust.PatchLstars`) through
  `ColorMath.GrayLevelOfLstar`, `Stats.PercentileSorted`; without classes the percentiles of the
  pixel luminance grey on `RasterOps.Subsample(r, Thresholds.TonalStride)`. The recipe of R-142
  (scale-down factor, strengths, `Maxima`). Evidence of R-143 re-measured on the curved
  subsample with `DustAnalyzer.PatchLstars`; class levels with the same statistics as the
  anchors; noise from `NoiseResult` times the slope at the sky; clipped shares.
- `Analysis/GamutAnalyzer.cs` (new, if step 0 allows): the round-trip table and the shares per
  class. `Parameters.DmaxProfilePath` (new, set in `WithDmaxFromProfile`), so the run can open
  the profile it took the Dmax from.
- `Input/FitsWriter.cs` (new, hand-written like the reader, ADR 0009): re-opens the source
  read-only, walks the HDUs as `FitsReader.LoadFrom` does but keeping the raw 2880-byte header
  blocks of the image HDU; writes the HDUs before it unchanged, its header with `HISTORY` cards
  inserted before `END` (a new block when the last is full), its data from the in-memory raster
  through the curve table, plane by plane, inverting the reader's normalisation (`BITPIX`,
  `BZERO`, `BSCALE`, the `BOTTOM-UP` flip), and the HDUs after it unchanged. For a non-FITS
  input: a minimal header, `BITPIX = -32`, `ROWORDER = 'TOP-DOWN'`. Writes to a temporary file
  in the target folder and moves it into place; refuses a target that resolves to the input
  path (`Path.GetFullPath`, case-insensitive); deletes the temporary file on failure.
- `Report/CurveTexts.cs` (new): the Romanian sentences, in the core: the evidence lines, the
  paper checks as `Recommendation`s with a `Because` (sky under L\* 15 or dust under paper
  black -> lift at the stretch; noise over `NoisePrintHiLsb`), the gamut note, the anchor-source
  sentence, the `HISTORY` text, the proposed file name.
- `Runs/CurveRun.cs` (new): `Execute(AnalysisResult, CurvePurpose, maxWidth, maxHeight)` ->
  `CurveSet { Anchors, Source, Problem, Suggestions, OriginalPreview }`, previews by
  `RasterOps.Subsample` at the integer stride that fits the size asked; `CurveExport.Save(
  AnalysisResult, CurveSuggestion, path)`.

Verified by `CurveTests` (13 tests, R-140 to R-145) going green; `scripts/verify.ps1
-SkipTests` green while editing.

## Step 2: window (`AstroPhotoAnalyzer.Wpf/`)

- `ViewModels/CurvesTab.cs` (new file; `ResultTabs.cs` is already long): `CurvesTabViewModel :
  ResultTabViewModel` with `IsPrint`/`IsScreen`, `Calculate` (runs `CurveRun.Execute` on a pool
  thread, own `IsBusy`), `Compute(w, h)` (the synchronous body the tests call), `Items` (name,
  frozen bitmap from `RasterBitmaps.Region`, evidence lines), `Current`, `Position`, `Next`,
  `Previous`, `OpenCurve`, `SaveAs`, events `CurveRequested` and `SaveRequested`, `Save(path)`,
  `SaveMessage`/`SaveError`, `AnchorSource`. Changing the purpose re-reads the evidence for the
  other purpose from the core and keeps the gallery. `CurveWindowViewModel`: `Rows` (label,
  input, output, L\* before and after, formatted with `Fmt`), `Chart`, `ShadowChart`,
  `ShadowLimit`, `Markers`, all sampled by the core.
- `Presentation/CurveCanvas.cs` (new): a `FrameworkElement` drawn in `OnRender`, after
  `BarsCanvas`: frame, grid, diagonal, polyline, points, vertical markers with rotated labels,
  an `XMax` property so the same control draws the whole curve and the shadows.
- `Views/CurvesView.xaml` (+ partial class in `ResultViews.xaml.cs`): radio buttons, button,
  the image in a `Viewbox Stretch="Uniform"`, `Inapoi` / `Inainte`, name and position, evidence,
  the two buttons. `Left` / `Right` as `KeyBinding`s on the view, which takes focus when the tab
  is selected; `Calculeaza` passes the gallery area's actual pixel size.
- `Views/CurveWindow.xaml` (+ code-behind in `OverlayWindows.xaml.cs`: dark title bar, Esc):
  table on the left, the two charts on the right.
- `MainViewModel`: the `Curves` tab in the constructor, `Populate`, `ClearTabs`.
  `MainWindow.xaml`: the seventh `TabItem`, `Curbe`. `MainWindow.xaml.cs`: `CurveRequested` ->
  `new CurveWindow { Owner = this, DataContext = m }.Show()`; `SaveRequested` -> a
  `SaveFileDialog` (`.fit`, the proposed name, the input's folder), then `tab.Save(path)`.
  `ParameterFields.TryBuild`: `DmaxProfilePath`.

Verified by `CurvesTabTests` (5 tests, R-146, R-147) and a `Render` line for `CurvesView` in
`WindowSmokeTests.R110_EveryTabRenders...`; then by running the window on
`starless_rbf0.5_stretch0.17.fit` and on `de_printat.png`: `Calculeaza`, arrows, `Curba...`,
`Salveaza ca...`, and the saved file opened again in the window (same WCS, classes found).

## Step 3: figures on the reference images

- `ValidationTests`: the kind `curves` (anchors, the points of each suggestion, the five L\*
  after, noise gain, clipped share; the header-card count and the hash-free "input unchanged"
  check of a save into a temporary folder). V-008 on the 0.17 FITS; V-009 on `de_printat.png`
  (no WCS: the histogram anchors). Copy the reference images into the worktree's
  `testdata/large/` first.
- Compare with the study: `Puternica` on the 0.17 FITS should give points near 0.306 > 0.401,
  0.543 > 0.753, 0.720 > 0.919 and L\* near 7.9 / 17.8 / 28.2 / 64.4 / 90.8. A difference beyond
  rounding between the C# build and the Python port is reported to the owner before the figures
  are recorded, not adjusted away.
- `/validate` for the existing cases V-001 to V-007: nothing may drift (the change adds, it
  moves no formula). Golden files: no diff expected; any diff is a finding.

## Step 4: records

- `docs/requirements.md`: R-140 to R-147 to `implemented` (with examples corrected to the
  build's figures); R-110 and R-111 for the seventh tab and the second run.
- Design notes, same task: `measurements.md` (curves, gamut), `input.md` (the writer),
  `thresholds.md`, `pipeline.md` (`CurveRun`), `window.md` (tab, window, canvas),
  `extending.md` (an output; "six tabs"), `code-map.md`, `known-limitations.md` (values
  outside 0..1 are saved clipped, as read; the ICC profile of a PNG is not carried; SIP cards
  are copied but still ignored by the classes), `testing.md`.
- `CLAUDE.md`: the first paragraph and the Layout section ("exports nothing", "six tabs", "both
  applications call `AnalysisRun.Execute` only"), per ADR 0031. `README.md` (Romanian): the
  `Curbe` tab under "Fereastra". `CHANGELOG.md`: Added. `docs/validation.md` and `.json`.
- The plan itself saved as `docs/plans/2026-10-05-suggest-s-curves-in-a-new-tab.md`.
- `/wrap-up`: gate green, the diff read back, commits on the task branch, the merge proposed.
  The change adds a feature, so it seems to warrant a minor version bump; that is the owner's.

## Order and size

0 (measurement) -> 1 (core; commit when `CurveTests` are green and the gate passes, which
needs the requirement statuses moved with the tests, since `RepoInvariantTests` refuses an
`R###_` test on a `draft` row) -> 2 (window) -> 3 -> 4. Steps 1 and 2 are the bulk: about eight
new source files and two views.

## Not in this task

A console flag; curves on the luminance; any lift of the sky in a curve; saving in the input's
format; black point compensation and the viewing light (the open follow-up of ADR 0030).

## What deviated (2026-10-05)

- A review by a subagent after the first commits found the save unsafe in two ways (another
  spelling of the input's path; a relative input and a changed working directory), a purpose
  change that recomputed everything on the UI thread and could show the wrong purpose's
  evidence, a test that asserted nothing, and behaviour without gate tests. All fixed on the
  branch with tests first; stale `CHECKSUM` cards are recorded as a known limit.

- Step 0 was run on single colours and a 17-cube grid, not on the 0.17 FITS per class; the
  per-class comparison came at step 3, from the build. The figures do not match the study's
  digit by digit because the definition differs (chroma only, no black point compensation);
  R-144 and ADR 0031 say so.
- The evidence reports the band and the bright clouds at the upper part of their classes, like
  the anchors, not at the class medians the plan's step 3 quoted (28.2 / 64.4); the anchors, the
  control points, the dust, the sky, the peaks, the noise gain and the clipped share match the
  study.
- `CurveTests` has 12 tests, not 13. Two of its scenes were corrected before their first green
  run: the band scene is used at 0.7 of its values (its peak patch sits at white), and the
  striped frame's peak stripe starts at x = 508 so that the stride-4 subsample sees it.
- `testing.md` gained a section; `console.md` a row for `DmaxProfilePath`; the smoke test also
  renders the curve window.
- The window was checked through the test renders and the view-model tests, not by hand in the
  running application: the arrow keys, the save dialog and the busy state of `Calculeaza` are
  the parts no test drives.
