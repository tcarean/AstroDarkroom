# Classify every frame: sky-relative classes without the band, one measurement, anchors from classes

Saved copy at the end of the task: `docs/plans/2026-10-06-classify-every-frame-relative-to-the-modelled-sky.md`.
Branch `claude/Classify-every-frame-relative-to-the-modelled-sky`, worktree
`.claude/worktrees/Classify-every-frame-relative-to-the-modelled-sky` (the kit's rule "Worktrees").

## Context

The five patch classes of ADR 0026 (dust, sky, band, bright clouds, peaks) exist only with a WCS
and the galactic band covering 10-90 % of the frame. Every other frame (no WCS, a deep-sky field
far from the plane or wholly inside it) falls back to the whole-frame rule of ADR 0025: dust =
the darkest 1 % of all patches, sky = the median of all patches, the map shows two layers, and
the curves take their upper anchors from three fixed pixel percentiles of the histogram
(94.2 / 99.1 / 99.9 %), calibrated on one image. On `Rosetta_starless_no_WCS.fit` the owner saw a
map with a few noise flecks called dust and nothing else, and curves whose "banda" and "nori
luminosi" points matched nothing on the map; on `Orion_with_WCS.fit` the curves change nothing.

Measured on 2026-10-06 with a scratch probe over the core (six images: Rosetta starless and with
stars, Orion starless and with stars, Andromeda, the 0.17 reference), the owner decided in chat:

1. The band classes stay exactly as they are when the WCS and the band allow them. Every other
   frame gets classes **relative to a modelled sky**: dust, sky, nebula, bright nebula, peaks.
2. The two upper thresholds come **from the image**, not from constants: nebula = above the sky
   model by more than the sky's own patch noise (median + k·σ of the residual, σ from the MAD,
   one clipping pass), bright nebula = above the Otsu split of the residuals over that. On the
   reference this reproduces the band classes without geometry (anchors 35.9 / 60.1 against
   33.2 / 57.7; bright 2.7 % against 2.0 %); on Rosetta it reproduces the curves the owner likes
   (32.2 / 47.1 against the histogram's 33.2 / 49.8). The fixed 5 / 10 L\* gave 26.3 / 43.5 on
   the reference.
3. **One measurement for both sources**: sky = median of the sky class; dust = the darkest
   extended area = min(the `DustClassFraction` (25 %) percentile of the dust class when it is
   non-empty, the darkest 1 % of the sky class). On the reference min(7.9, 10.1) = 7.9,
   unchanged; on Rosetta the 31-patch dust artefact at 8.4 loses to the sky at 6.1 (today 6.2);
   Orion 10.8 (11.0); Andromeda 0.0 (0.0). The whole-frame rule and the `NoDust` branch go.
4. The curve anchors come from the classes **always**; the point labels take the names of the
   source's classes. `CurveAnchorSource` and the three histogram percentiles go.
5. Peaks already at or above `CurvePeakTarget` (0.92): a **red sentence** in Curbe and no
   suggestion at all (Orion's peaks are at 0.96: the recipe never lowers a point, so there is no
   room). The "shoulder" (an S-curve compressing the highlights) is recorded as rejected for now.

Still to measure inside the task (step 0): a 3 x 3 median on the patch L\*s before the
sky-relative classification, against the star speckles on the with-stars images.

Settled during the task (recorded in ADR 0033): the median was measured and rejected (it moves
the anchors by up to 2 L\* and leaves the speckles); k stays 4; the dust class decides only
from `DustClassMinFraction` (5 %) of the patches, because on `de_printat.png` the fallback's
dust class is the Pipe's cores (0.8 %, p25 at L\* 2.5), which ADR 0025 rejected; the band
scene's dust blob grew to a radius of 80 px to clear that floor; V-010 to V-012 replaced the
planned V-010 / V-011 (an analysis case and a curves case on Rosetta, a curves case on Orion).

## Step 0: worktree, data, two measurements

- `git worktree add .claude/worktrees/Classify-every-frame-relative-to-the-modelled-sky -b claude/Classify-every-frame-relative-to-the-modelled-sky main`;
  copy `de_printat.png`, `starless_rbf0.5_stretch0.17.fit`, the two ICC profiles and the four
  new FITS (`Rosetta_starless_no_WCS.fit`, `Rosetta_withStars_no_WCS.fit`, `Orion_with_WCS.fit`,
  `Orion_WithStars_and_WCS.fit`) into the worktree's `AstroPhotoAnalyzer.Tests/testdata/large/`.
- Reuse the scratch probe (`scratchpad/classprobe`, outside the repo) to measure, on the six
  images: (a) the sky-relative classes with and without a 3 x 3 median of the patch L\*s before
  classification (fractions, the five anchors, the map PNGs); adopt the median only if it removes
  the star speckles on the with-stars images and moves no anchor by more than 1 L\* on the
  starless ones; otherwise it is a known limit. (b) The k of the noise threshold: 3, 4, 5 on
  the one-pass σ; keep 4 unless the fractions say otherwise (they moved 2 % between 3 and 5 on
  Rosetta). The figures go into the ADR.

## Step 1: requirements first (`docs/requirements.md`)

Rewrite, status `draft` until the tests carry them: R-044 (the two class sources, the
sky-relative thresholds, the one measurement with the min rule, the method names), R-092 (the
Because names the winner: `praful (percentila 25 % a dalelor de praf ...)` or `praful (cel mai
intunecat 1 % al dalelor de cer ...)`), R-113 (the definition sentence per source), R-130
(always five layers, names per source, the legend with the per-image thresholds), R-141 (anchors
from the classes always; an empty upper class is a problem), R-142 (peaks at or over
`CurvePeakTarget`: a problem, no suggestion), R-146 (the anchor sentence per source; the
problem sentence in red), R-147 (labels per source). Examples with the numbers of step 0 and of
V-010 / V-011 below.

## Step 2: core, numbers (`AstroPhotoAnalyzer.Core/`)

**`Thresholds.cs`**: add `SkyModelLowPercentile` 25 (the sky model without the band takes this
percentile of each cell, so an object covering half a cell does not lift it), `SkyNoiseClipK` 3
(the clipping pass) and `SkyNoiseK` 4 (the nebula threshold in σ); rename `DustAreaFraction` to
`SkyDarkestFraction` 0.01 with the new meaning (the darkest share of the sky class); remove
`CurveBandPercentile`, `CurveCloudPercentile`, `CurvePeakPercentile`. Each comment says where the
value came from (the six images of 2026-10-06).

**`Input/Stats.cs`**: `OtsuThreshold(float[] sorted)` (the split maximising the between-class
variance; NaN under two values) and `RobustSigma(float[] values, out double median)`
(1.4826 x MAD). Unit tests in `StatsTests`.

**`Analysis/DustAnalyzer.cs`**:

- `DustMethod` becomes `{ Band, Sky }` (how the classes were made); `DustMap.SkyReason`
  `{ None, NoWcs, NoBand, AllBand }` says why `Sky` was used. `HasClasses` / `UsesClasses` and
  `NoDust` go: every map has classes. `DustMap` also carries `SkySigma`, `NebulaThreshold`,
  `BrightThreshold` (L\* over the model; NaN for `Band`) for the legend and the report.
- `SkyModel(L, band, cols, rows, percentile)`: the existing function with the cell statistic as a
  parameter (50 for the band, `SkyModelLowPercentile` for the fallback); with `band` all false
  every cell has its patches.
- `ClassifyBySky(map, L)`: residual = L - model; median and σ (`RobustSigma`), one clipping pass
  at `SkyNoiseClipK` σ, median and σ again; dust under median - `SkyNoiseK` σ, nebula over
  median + `SkyNoiseK` σ, bright nebula over `OtsuThreshold` of the residuals above that
  (none when fewer than two), peaks the brightest `PeakFraction` as today (applied last). The
  `PatchClass` enum keeps its five values; `Band` and `BrightCloud` mean nebula and bright
  nebula under `Sky` (the names are a function of the method, below).
- `Build`: without a WCS, or when `Classify` finds the band under or over `BandMinFraction`, it
  calls `ClassifyBySky` and sets the reason; the band path is untouched.
- `Measure(patchLstars, map, paperBlack)`: one path. `LstarSky` = median of the sky class
  (median of all patches only when the class is empty); `LstarDust` = min(p25 of the dust class
  when non-empty, p1 of the sky class); `DustStats.DustSource { DustClass, DarkestSky }` says
  which won, `DustPatchCount` the count behind it. Remove `Classes(map, asIs)`: the map window
  paints `map.Classes`.
- `ClassNames(DustMethod)`: `praf, cer, banda, nori luminosi, varfuri` for `Band`;
  `praf, cer, nebuloasa, nebuloasa stralucitoare, varfuri` for `Sky`. The one place both the
  map, the curve labels and the report take the names from.

**`Analysis/PrintStretchSimulator.cs`**: `DustDescription` by `AsIsDust.DustSource` and the
method (`percentila 25 % a dalelor de praf (cu 5 L* sub nivelul local al benzii sau sub cerul
modelat; 9.4 % din cadru)` / `cel mai intunecat 1 % al dalelor de cer (clase relative la cerul
modelat: nebuloasa peste el cu 5.9 L*, 4 sigma ale cerului)`); `SkyDescription` per method;
`MethodNote` from `SkyReason` (`fara WCS`, `fara banda galactica in cadru`, `cadrul e in
intregime in banda`; null for `Band`); `ClassFractions` always, with `ClassNames`.

**`Analysis/CurveAdvisor.cs`**: remove `CurveAnchorSource`, `SourceOf`, `SortedLuminanceGray`;
`ClassLevels(sub, map, paperBlack)` takes the upper anchors from the classes always;
`Labels(map)` from `ClassNames`. `Problem(anchors, labels)` adds two cases: an empty upper class
(`CurveTexts.ClassEmpty(label)`) and the peaks anchor at or above `CurvePeakTarget`
(`CurveTexts.PeaksOverTarget(value)`), checked before the increasing test. `Measure` loses the
source parameter. `Runs/CurveRun.cs`: `CurveSet.Source` goes; `byClass` is always true for the
gamut; `SourceSentence` from `CurveTexts.AnchorSource(map)`.

**`Report/CurveTexts.cs`**: `PointsNote(map, colour)` and `AnchorSource(map)` per method (`Ancorele
vin din clasele tabului Tonuri: ...` naming the method and, for `Sky`, the reason); new
`PeaksOverTarget` (`Varfurile sunt deja la 0.962, la sau peste tinta 0.92: nu mai e loc de
contrast fara sa cobori ceva, si curbele sugerate nu coboara nimic. Imaginea e deja la plafon.`)
and `ClassEmpty`. `Report/ReportWriter.cs` and `Recommendations.cs`: the definition sentence and
the Because follow `DustDescription`; the `clase:` line is always printed.

## Step 3: window (`AstroPhotoAnalyzer.Wpf/`)

- `ViewModels/MapViewModel.cs`: always five layers, names from `ClassNames`; `LegendOf` per
  method, with the per-image thresholds interpolated from the map (`nebuloasa: peste cerul
  modelat cu 5.9 L* (4 sigma ale cerului)`, `nebuloasa stralucitoare: peste 17.6 L* (pragul Otsu)`);
  `NoWcsWarning` stays (the clouds are still not identified without a WCS).
- `ViewModels/ResultTabs.cs`: the guide paragraph "Praful si cerul" rewritten for the two
  sources, every number interpolated (`SkyNoiseK`, `SkyDarkestFraction`, `DustClassFraction`).
- `ViewModels/CurvesTab.cs`: `IsProblem` (true when the shown `Message` is a core problem, false
  for `Computing` and exceptions); labels from the core's set. `Views/CurvesView.xaml`: a
  `DataTrigger` on `IsProblem` paints the message with `BadBr`, like the paper checks.
  `CurveWindowViewModel`: labels from the suggestion's points (already carried).

## Step 4: tests (`AstroPhotoAnalyzer.Tests`, `AstroPhotoAnalyzer.Wpf.Tests`)

- `Scenes.cs` / `WcsFixtures.cs`: a deep-sky scene, `NebulaScene()`: a gradient sky with noise
  (the `DarkSky` recipe), a wide Gaussian halo and a bright core, no WCS; a variant
  `WithStars`. `BandScene` stays for the band path.
- `DustAnalyzerTests`: rewrite the four fallback tests as R-044: the sky-relative classes follow
  the thresholds (every nebula patch over model + threshold, the core bright, the peak count);
  the thresholds come from the image (a scene with twice the noise has about twice the nebula
  threshold); the one measurement (dust = min rule, with a scene where the dust class is an
  artefact brighter than the darkest sky, and one with a real dark lane); `Band` unchanged on
  `BandScene`, `NoDust` gone (`BandScene(withDust: false)` now gives the sky's darkest 1 %).
- `StatsTests`: Otsu on a bimodal array, `RobustSigma` on a known sample.
- `PrintStretchSimulatorTests`, `BackgroundRecommendationTests`, `WcsBorrowTests`: the new
  descriptions and method notes (R-044, R-092, R-027).
- `CurveTests`: R-141 on the fallback classes (the anchors are the classes' upper parts, no
  histogram); R-142 peaks over the target give a `Problem` and no suggestion; an empty upper
  class gives a `Problem`; `R144` gamut per class on both sources.
- `CurvesTabTests`: R-146 the problem sentence is marked (`IsProblem`), `AnchorSource` names the
  method; R-147 labels `nebuloasa` on a frame without a WCS. `OverlayWindowTests`: R-113 and
  R-130 definition and legend texts, five layers without a WCS. `ViewModelTests`: the guide text.
- Goldens: `dark-sky`, `clean-sky-canvas`, `mono-png8`, `dark-sky-wcs` change (dust and sky L\*,
  the definition sentence, a `clase:` line, the Because). Regenerate with `APA_UPDATE_GOLDEN=1`,
  read every changed line and tie it to this task (the rule "Golden files").
- `RepoInvariantTests` keeps IDs and statuses in step: the rows go to `implemented` when the
  tests carry them.

## Step 5: figures (`docs/validation.json`, `.md`, `testdata/manifest.json`)

- Re-measure with `/validate`: V-001, V-002, V-003, V-005 (`simulation.method` `Sky`, the dust
  and sky L\*, the recommended target), V-009 (`curves.source` key removed; anchors from the
  classes). V-004 and V-008 must not move: the band path is untouched; a moved figure there is a
  finding, not a new value.
- Add `Rosetta_starless_no_WCS.fit` and `Orion_with_WCS.fit` to the manifest
  (`scripts/check-testdata.ps1 -Update`) and two cases: V-010 (Rosetta: the sky classes, the
  dust from the darkest sky, the curves and their anchors) and V-011 (Orion: `curves.problem`
  = the peaks sentence, `curves.count` 0). History lines in `validation.md` with the old and new
  values and why.

## Step 6: docs

- New ADR 0033 "Without the band, the classes are relative to the modelled sky with thresholds
  from the image, and one measurement serves both sources": the six-image measurements of
  2026-10-06 (the table of σ, thresholds, fractions, anchors; the min rule's numbers; Orion's
  0.962), the alternatives rejected (fixed 5 / 10 L\*; histogram percentiles as classes; the
  shoulder; a flag for a "real" dust class instead of the min). Status lines on ADR 0025
  (decision 4 superseded), ADR 0026 (the fallback paragraph superseded) and ADR 0032 (the
  histogram anchors superseded), in the way the kit's ADR 0024 allows.
- `docs/design/measurements.md` (B5 and the curves sections), `thresholds.md` (the rows),
  `recommendations-and-report.md` (the Because and the definition sentence), `window.md` (map
  layers, the red curve message), `known-limitations.md` (the frame-rule bullets replaced: the
  star speckles if the median is not adopted; an object filling the frame makes the sky model
  the faintest nebulosity, said as such; the Rift is found only with the band), `code-map.md` if a
  file is added. `scripts/check-design.ps1 -Since main` lists what the branch touched.
- `README.md` (the map paragraph, the curves paragraph, the "fara WCS" sentence of the dust
  section), `CHANGELOG.md` under `[Unreleased]` (Changed: the fallback classification and the
  single measurement; Added: the red sentence for peaks at the ceiling; Removed: the histogram
  anchors).

## Verification

- `scripts/verify.ps1` green (gate: format, build with warnings as errors, both test projects,
  design index, test-data hashes).
- `dotnet test AstroPhotoAnalyzer.sln -c Release --filter "Category=Validation"` with the
  drift table pasted: V-004 / V-008 unchanged, the others re-recorded, V-010 / V-011 new.
- Run the window on `Rosetta_starless_no_WCS.fit`: Tonuri shows `clase relative la cerul
  modelat`, the map has five layers on the nebula, Curbe has curves with `nebuloasa` points at
  the places the map paints; on `Orion_with_WCS.fit`: Curbe shows the red sentence and nothing
  else; on `starless_rbf0.5_stretch0.17.fit`: everything as before (band classes, V-008 curves).
- Console on the three: the definition sentence and the `clase:` line.
- `/wrap-up`: CHANGELOG, requirements to `implemented`, the plan saved under `docs/plans/`,
  commit on the task branch, merge proposed.
