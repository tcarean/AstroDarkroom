# Judge walking noise by the drift, notice the darks, measure the sensor pattern

Saved to `docs/plans/2026-10-10-judge-walking-noise-by-the-drift-and-measure-the-sensor-pattern.md`
once approved. Branch `claude/Judge-walking-noise-by-the-drift-and-measure-the-sensor-pattern`, in its
own worktree from `main` (the open branch `claude/Make-the-SynScan-arrows-symbols-and-configurable`
also touches `SeriesReport.cs`; whichever merges second resolves the conflict, the owner picks the order).

## Context

On 2026-10-10 the owner compared two stacks: Veil (135 mm, 87 frames, worms visible) and the Milky
Way at Smida (20 mm, 32 frames, clean). LightsCheck called the dithering INSUFICIENT on both and
"groaznic" on Smida, although the owner dithered every frame there. The measurements of the session
(scratchpad scripts, not in the repo) explained it:

| | Veil | Smida |
| --- | --- | --- |
| scale | 5.99 "/px | 40.4 "/px |
| drift, arcseconds | -1.6 / -2.9 "/min | -2.2 / -2.8 "/min (the same mount) |
| drift per 2 min exposure | 1.1-1.3 px | 0.15 px |
| longest run at 10 px | 10 frames (61-70) | 12 frames (9-20) |
| expected dash = drift per exposure x (run - 1) | ~11 px | ~1.9 px |
| darks in the session | none | 20, master dark subtracted |
| warm pixels in the calibrated frames, > 3 sigma of one frame | 1162 / Mpx | (dark removed; its master had 34 / Mpx) |
| ACF anisotropy of the stack's background, G, lags 3-48 | 1.45 along Y = RA | 1.15 along X |

So: the mount drifts the same on both nights; at 20 mm the pattern does not move between frames
(no walking noise regardless of the dither), at 135 mm it moves over a pixel per frame and, without
darks, the warm pixels of ISO 1600 become dashes along RA inside every run of frames at one spot.
"Agresiv" means every frame and both axes, not larger. The application has the drift, the runs, the
scale and the exposure; it lacks the darks folder, the per-exposure drift, a verdict that uses it, the
sensor pattern, and the words. This task adds all of them (the owner chose A and B in one branch).
The stack ACF measurement stays out: it is confounded by the stars' RA elongation and discriminated
weakly (1.45 against 1.15); recorded in the ADR as rejected for now.

## What changes, where

Everything computed goes in `src/LightsCheck.Core` (`Analysis/`, `Thresholds.cs`), every word in
`Report/`; the window binds rows (ADR 0008, 0012). Read `docs/design/series-analysis.md`,
`session-and-console.md`, `window.md` before editing.

### 1. The darks folder is noticed, never read (R-040)

- `Session/SessionFolder.cs`: `DarksName = "darks"`, `DarksDir?`, `Darks` list, found like `biases/`
  (`FramesIn`). Nothing is read from it.
- `Report/InventoryReport.cs` `WriteHeader`: a line `darks : darks\ (20 cadre .NEF)` or `(lipsa)` after
  `biases`. The window's SESIUNE card parses the header, so it shows up by itself.
- Tests: `SessionFixture.WithStarSeries(..., darks: n)` and `WithLights(..., darks: n)` write `darks/`
  frames (any synthetic frame; never read). `R040_` in `SessionFolderTests`/`InventoryTests`.
- Goldens: `inventory.txt`, `series.txt`, `series-classes.txt` gain the one header line.

### 2. Drift per exposure, the expected dash, the three-way walking verdict (R-041)

- `Analysis/SeriesAnalyzer.cs`, new fields of `SeriesAnalysis`:
  - `DriftPerExposurePx` = hypot(DriftXPerMin, DriftYPerMin) x median exposure (s) / 60; null without drift.
  - `DriftAxis`: the image axis carrying the larger drift component, worded as RA/DEC when the RA axis is
    known (`RaAxis`), else X/Y (the report words it).
  - `WalkingStreakPx` = DriftPerExposurePx x (longest run at `Th.CoverageLoose` - 1), 0 without a run;
    null without drift.
  - `WalkingNoise` enum `{ Unknown, Ok, Static, Insufficient }`: `Unknown` when registered < 2;
    `Ok` when `DitherAnalysis.WalkingNoiseOk` (the ADR 0007 rule, unchanged); else `Static` when
    `WalkingStreakPx` is known and under `Th.WalkingStreakVisible`; else `Insufficient`.
  `DitherAnalysis.WalkingNoiseOk` stays as it is (the dither-only rule); `SeriesAnalyzer` combines.
- `Thresholds.cs`: `WalkingStreakVisible = 3.0` px, doc comment: a rule of thumb placed between the
  measured 1.9 px of Smida (nothing visible) and 11 px of Veil (worms), to be tuned on more sessions.
- `Report/SeriesReport.cs`:
  - `DriftRows`: new row `drift intr-o expunere : 1.26 px (= 7.6") pe 120 s; dare de walking noise de
    pana la 11 px pe RA (cel mai lung grup la 10 px: 10 cadre)`; `-` parts when unknown.
  - `DitherVerdict(SeriesResult)` (takes the series now): walking part `OK pentru walking noise` /
    `FARA walking noise (driftul de 0.17 px pe expunere nu misca pattern-ul: dare de cel mult 2 px)` /
    `INSUFICIENT pentru walking noise (dare de pana la 11 px pe RA)`; the two parts are ordered
    worst first (INSUFICIENT / ATENTIE before OK / FARA) so the chip colour follows the worst.
  - `Wpf/Presentation/VerdictPalette.cs`: `FARA` is a good word (green), like OK.
- Tests: `R041_` in `SeriesAnalyzerTests` on hand-made inputs (drift + runs -> streak and verdict, the
  three outcomes, null cases) and one end-to-end in `SeriesTests` on a fixture pattern with a known
  drift (frames stepping 1 px without a pause, in a run). Goldens: the DRIFT row and the verdict words.

### 3. The scale row in DITHERING (R-042)

- `SeriesReport.DitherRows` and `DitherScalarRows`, before `verdict`: `scara : 1 px = 5.99"; o miscare
  de 5 px (pragul) cere 30" pe cer, una de 10 px 60"` (from `ArcsecPerPixel`, `Th.DitherStep`,
  `Th.CoverageLoose`); `necunoscuta (fara focala)` without a scale. `MainViewModelTests` asserts the
  `DitherRows` labels equal `DitherScalarRows` and `verdict` is last: keep both.
- Test `R042_` in `SeriesReportTests` (words from a fixture result); goldens gain the row.

### 4. The explanation (R-043)

- `SeriesReport.DitherExplanation`: `ReportParagraph`s built from `Th`, Romanian, in this order:
  (1) what walking noise is (the sensor's fixed pattern, moved by the drift between dithers, averaged
  into dashes along the drift); (2) the scale: the same mount motion and the same drift are
  6.7 times fewer pixels at 20 mm than at 135 mm, so a short focal length forgives and a long one
  needs the dither every frame; (3) what helps: a move every frame, on both axes, random direction,
  the frequency matters more than the size (5 px is enough when no two neighbours share a spot);
  (4) the darks: they remove the pattern; taken without touching the lens or the filter (lens off,
  body cap on, viewfinder closed, same ISO, exposure and temperature, at the end of the night);
  (5) where the figures come from (drift per exposure, the longest run, the pattern row), with the
  emphasis on (3).
- Window: `MainViewModel.DitherExplanation` (like `RejectedExplanation`), rendered in
  `Views/DitherView.xaml` under the DITHERING card's rows, same `ParagraphVm` template as
  `RejectedView.xaml`. The console does not print it (ADR 0012).
- `README.md`: the DRIFT and DITHERING bullets grow the same content, plus the `pattern fix` row and
  the darks line of SESIUNE.
- Tests: `R043_` in `SeriesReportTests` (paragraph count, the emphasised one, the thresholds quoted)
  and in `MainViewModelTests`; `WindowSmokeTests` covers the render.

### 5. The sensor pattern from the lights (R-044)

- `Runs/FrameRun.Measure` returns, beside the measurement, the central crop of the raw mosaic:
  `Th.PatternCrop` = 1024 px square (smaller on a small frame: the fixture's 640 x 480 gives 480 x 480),
  stored as `ushort[]` ADU (2 MB per 24 MP frame; 87 frames = 175 MB beside the 1.6 GB peak of the
  parallel decode). The crop starts at an even row and column so the CFA phase is the frame's.
- `Analysis/PatternAnalyzer.cs`, `PatternAnalyzer.Analyze(crops, cfa, frameSigmas, dither)` ->
  `SensorPattern`: the per-pixel median across the frames (the frames are not registered: the sky and
  the stars move, the sensor stays), minus the black; per CFA position the pixel minus its 5 x 5
  same-colour median (factor `NoiseAnalyzer.HighPass(Raster, py, px)` out of `HighPassSigma`, which
  becomes `ClippedStd(HighPass(...))`); against the one-frame noise per position (the median over the
  frames of `ChannelLevel.HighPassSigma`): `WarmPerMpx` (above `Th.PatternWarmSigma` = 3),
  `HotPerMpx` (above `Th.PatternHotSigma` = 10), `PatternSigma` per position, `Frames`.
  Valid only when the median is clean of stars: at least `Th.PatternMinFrames` (7) frames and the
  largest cluster at `Th.CoverageTight` (3 px) under half the registered frames (a pixel then sees a
  star in fewer than half the frames); else `null` with a reason word.
- `SeriesRun`: collects the crops in the parallel loop, calls the analyzer after `DitherAnalyzer`,
  drops the crops; `SeriesResult.Pattern`.
- `SeriesReport`: row `pattern fix` in DITHERING (console and window, before `verdict`):
  `1162 pixeli calzi / Mpx peste 3 sigma, 7 fierbinti peste 10 (mediana a 87 cadre neinregistrate);
  fara dark-uri in sesiune: raman in cadre` or `; 20 dark-uri in darks\, Siril le scade`; or
  `nedeterminat (cadrele au stat pe loc: stelele raman in mediana)` / `(sub 7 cadre)`. The darks
  clause comes from step 1. The CSV is per frame and does not change.
- Tests: `SyntheticFrame` gains `HotPixels` (a fixed list of (x, y, ADU) added after `Pixel()`),
  `SessionFixture` a `sensorPattern` option that puts the same 200 warm (+5 sigma) and 20 hot
  (+40 sigma) pixels on every light; `R044_` in `PatternAnalyzerTests` (counts recovered within
  10 %, the null cases, the CFA phase) and `SeriesTests` end to end; the existing goldens get the
  row with its `nedeterminat` or measured words (the 8-frame pattern has 7+ frames and moves, so it
  measures: tie the numbers).

### 6. Validation (the rule "Measured on the sessions")

- `docs/validation.json` / `.md`: the `series` kind records `driftPerExposurePx`, `walkingStreakPx`,
  `walkingNoise` (word), `darks` (count), `patternWarmPerMpx`, `patternHotPerMpx`; the `inventory`
  kind records `darks`. V-008 (Veil) gets the new figures after the run; new cases V-012 Smida
  inventory and V-013 Smida series (`Milkyway-Core/2026-08-12-Smida`, under `E:\Photografie\Astro`,
  the folder `LIGHTSCHECK_TESTDATA_LARGE` must name). Expected words: Veil `INSUFICIENT ... dare de
  pana la ~11 px pe RA`, Smida `FARA walking noise ... dare de cel mult ~2 px` with `ATENTIE la
  pixeli calzi`. The Veil pattern figure from the raws is new (the session's 1162 / Mpx was on the
  calibrated frames); record what the run measures and compare with it in the ADR.
- `/validate` runs on both sessions before the end; the drift table is pasted at wrap-up.

### 7. Docs

- `docs/requirements.md`: R-040 to R-044 as above, `implemented`, tests named `R040_`..`R044_`;
  R-002 mentions `darks/`; R-021's verdict sentence points at R-041; R-027 lists the new rows.
- `docs/decisions/0014-the-walking-noise-verdict-reads-the-drift-per-exposure-and-the-sensor-pattern-comes-from-the-lights-median.md`:
  the measurements above, the thresholds as rules of thumb (3 px dash, 3 and 10 sigma, 7 frames, the
  half-cluster validity rule), the alternatives rejected (the stack's ACF: confounded by the RA
  elongation and weak; reading the darks: the lights already carry the pattern and the darks may be
  absent; a dither verdict in arcseconds: the sensor sees pixels; rejecting frames: ADR 0011).
- `docs/design/series-analysis.md`: sections for the verdict combination, the pattern analyzer and
  the new rows; `Covers:` adds `PatternAnalyzer.cs`. `session-and-console.md`: the darks folder.
  `window.md`: the explanation under DITHERING. `docs/design/README.md` index rows updated.
- `CHANGELOG.md` `[Unreleased]`: Added (darks line, drift per exposure and the dash, the three-way
  verdict, the scale row, the explanation, the pattern row), Changed (the verdict parts ordered worst
  first). No version bump (the owner's).
- `README.md` as in step 4.

## Order of work

1 (darks) -> 2 (verdict) -> 3 (scale row) -> 4 (explanation) -> 5 (pattern) -> goldens regenerated
and every changed line tied -> 6 (validation on both sessions) -> 7 (docs) -> `/wrap-up`.
Small commits on the branch after each step with the gate green.

## Verification

- Gate: `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify.ps1` green.
- Goldens: `LIGHTSCHECK_UPDATE_GOLDEN=1 dotnet test LightsCheck.sln --filter GoldenOutputTests`, then
  read the whole diff: expected lines are the `darks` header line, the `drift intr-o expunere` row,
  the `scara` and `pattern fix` rows, the verdict words and their order. Anything else is a finding.
- Real sessions: `lightscheck E:\Photografie\Astro\Veil\2026-10-06-3h` and
  `lightscheck E:\Photografie\Astro\Milkyway-Core\2026-08-12-Smida`; check the words above and that
  the Veil run stays within a few seconds and ~200 MB of today's.
- `/validate` with `LIGHTSCHECK_TESTDATA_LARGE=E:\Photografie\Astro`: V-001..V-013 green, drift
  table pasted.
- Window: `dotnet test tests/LightsCheck.Wpf.Tests` with `LIGHTSCHECK_DUMP_SCREENS` set, look at the
  Dithering and Rezumat screens (the explanation, the new rows, the chip colours).

## What deviated

- The IDs moved: the SynScan-arrows branch merged into `main` before this one took R-040 and
  R-041 and ADR 0014, so the darks are R-042, the verdict R-043, the scale row R-044, the
  explanation R-045, the pattern R-046, the window R-047, and the decision is ADR 0015.
- The pattern crop is 512 px of floats (1 MB per frame), not 1024 px of `ushort`: a Siril
  float FITS (0..1) would collapse to 0 or 1 as `ushort`, and 262 k pixels count a thousand warm
  pixels per megapixel to a few percent.
- The pattern refuses when fewer than two frames are registered (a fourth status), since the
  cluster rule needs positions.
- The classes golden gained `darks: 2` but not the synthetic sensor pattern: hot pixels on the
  lights are detected as stars and would have moved the frame figures of that golden for a
  reason unrelated to the task; the measured pattern is pinned by `R046` on the eight-frame
  fixture instead.
- Without a run of three frames the words say `nicio dara de walking noise (niciun grup de 3+
  cadre consecutive la 10 px)` instead of a dash of 0.0 px.
- The verdict words stay short (`INSUFICIENT pentru walking noise, OK pentru pixeli calzi`),
  since the window's chip shows them whole and a parenthesis of 80 characters did not fit;
  the dash, the drift, the run and the hot-pixel rule open the verdict's text instead.
- The `pattern fix` row reports the pattern's sigma per channel (ADU and share of a frame's
  noise) and the hot pixels, not a count of warm pixels at 3 sigma: measured on the Veil raws
  after the first commit, that count moved from 575 to 126 per megapixel with the noise
  estimator (MAD against the clipped standard deviation) and read 1162 on the debayered
  green channel, because it sat on the pattern's steep tail; the owner chose the sigma. A row
  `pattern in stiva` with the surviving fraction of R-048 was added at the owner's request.
- Measured on the real sessions (the console of this branch): Veil `INSUFICIENT ... dare de
  pana la 9.9 px pe RA: drift 1.10 px pe expunere, grupul cel mai lung la 10 px 10 cadre`,
  pattern 126 warm and 4 hot pixels per megapixel from the raws (the 1162 of the scratch
  measurement was on the debayered green channel of the calibrated frames); Smida `ATENTIE la
  pixeli calzi ..., FARA walking noise (dare de cel mult 1.9 px: drift 0.17 px pe expunere,
  grupul cel mai lung la 10 px 12 cadre)`, pattern 744 warm and 55 hot per megapixel, 20 darks. After the owner's choice of the sigma: Veil `sigma 1.4 ADU pe R (12 %), 2.1 pe G1 (10 %), 2.1 pe G2 (10 %), 2.3 pe B (10 %)`, 1.6 % of the pattern in the stack (16 pairs); Smida `1.1 / 1.2 / 1.2 / 0.9 ADU (21 / 19 / 18 / 16 %)`, 23.4 % (104 pairs). The median's noise is measured from the odd and even medians, not from the Gaussian formula, which over-subtracted on the eight-frame fixture.
