# Fix the defects the analyzer review found

Status: approved 2026-10-23. Branch `claude/Fix-the-defects-the-analyzer-review-found`, worktree
`.claude/worktrees/Fix-the-defects-the-analyzer-review-found` (the kit's rule "Worktrees").
The plan the owner approved on 2026-10-23; the section "Record" at the end holds what was
measured while it ran.

## Context

The code review of area 2 (`docs/plans/2026-10-22-document-every-type-and-method-and-translate-the-comments.md`,
"Code review of area 2", items 1-15) found fifteen code defects on `main` and left them as
questions. The owner answered on 2026-10-23: "astea vrem sa le fixam pe toate, conform
recomandarilor tale", accepting the recommendation of each item as the task brief restates it.
This task fixes all fifteen, in the order the owner set: speed first (15, results identical,
proved by `/validate`), then the report's texts (1, 2, 4, 5, 6+8, 7), then the measurements'
precision (3, 10, 12, 13, 14, then 9), then the documentation line (11). Area 1's questions are
not part of it; area 3 of the documentation backfill (Report/, Runs/) must not run in parallel.

Rules that bind every step: requirements first (`docs/requirements.md` row edited, then a
failing `R###_` test, then the code); a decision with a why gets an ADR quoting the owner's
answer; every type or method added or edited gets a full XML doc comment; comments English in
`Core/Analysis/` and `Core/Input/`, Romanian without diacritics in `Report/`, `Runs/`,
`Parameters.cs`, `Thresholds.cs` and the window; user-facing strings Romanian without
diacritics; goldens change only where a fix means to, every changed line reported with its
cause (the kit's ADR 0033); `docs/validation.json` changes only after the owner approves the
drift table.

## Step 0. Worktree, data, baseline

1. From `E:\Photografie\Code\AstroPhotoAnalyzer`:
   `git worktree add .claude/worktrees/Fix-the-defects-the-analyzer-review-found -b claude/Fix-the-defects-the-analyzer-review-found main`.
   The worktree holds the whole `photo` repo; the project is `<wt>/AstroPhotoAnalyzer/`.
2. Copy the seven files of `AstroPhotoAnalyzer.Tests/testdata/manifest.json` from the main
   checkout's `AstroPhotoAnalyzer.Tests/testdata/large/` (all present, checked) into the
   worktree's (772 MB); run `scripts/check-testdata.ps1`.
3. Baseline: `dotnet build -c Release`; `APA_VALIDATION_DUMP=<scratch>/baseline.jsonl dotnet test
   ... --filter "Category=Validation"` (the full figure set, to diff against after each speed
   commit); console run time on `de_printat.png` and the plate-solved FITS (median of three).
4. Copy (never edit) the review probes from the earlier session's scratchpad (`probe/`,
   `review/v1-verify ... v6-verifier`, `effprobe`, `wrap-probe`; all still present) into this
   session's scratchpad for the measurements of items 9, 10 and 15.

## Step 1. Speed, results identical (item 15), two commits

**15b. Selection instead of full sorts** (`Core/Input/Stats.cs`, English comments).
- Add `Stats.Percentile(float[] values, double p)` and `Stats.Percentiles(float[] values,
  double[] ps)`: copy once, quickselect (median-of-three pivot, iterative) the ranks numpy's
  rule needs (`lo = floor(p/100 (n-1))`, `hi = lo+1`), selecting the ranks in increasing order on
  the shrinking right part, and interpolate exactly as `PercentileSorted` does. A vector that
  holds a NaN falls back to `PercentileSorted(Sorted(v), p)` (Array.Sort puts NaN first; the
  readers let none through, R-013). `Median(values)` becomes `Percentile(values, 50)`.
  `PercentileSorted` and `Sorted` stay for callers that need several percentiles of one sorted
  vector (DustAnalyzer.Measure's class sets stay as they are: the owner left the smaller ones).
- Callers rewritten: `TonalAnalyzer.Analyze` (the seven reference percentiles through
  `Percentiles`, the background median through `Median`), `BackgroundAnalyzer.Analyze` (the
  luminance cut through `Percentile`, the per-plane and per-region medians through `Median`),
  `DustAnalyzer.MedianFilter` (`Median`, already; now selection).
- Tests: `StatsTests` — `Percentile`/`Percentiles` equal `PercentileSorted(Sorted(v), p)` on
  seeded random vectors with duplicates, all-equal vectors, n = 1, 2, 3, and a vector with a NaN;
  every rank of `TonalResult.PercentileRanks`.
- Proof: `/validate` dump byte-identical to the baseline (all figures; the owner says 1237);
  console timings before and after, pasted in the plan's record and `measurements.md`.
- Commit: `Read the medians and percentiles by selection, not by sorting the whole vector`.

**15a. The ladder stops early** (`Core/Analysis/PrintStretchSimulator.cs`).
- The series the simulator reads: the dust against the three paper blacks (Dmax, Dmax ± step),
  the sky against `LowSeparationLstar`, each row cloud against paper black. Keep a `crossed`
  flag per series (first step at or above its level, exactly `Crossing`'s test) and a count of
  table rows still to build (grid rows and the given target). After a step, when every series
  has crossed and no row is left, leave the loop; the series arrays are truncated to the steps
  computed and `Crossing` reads them unchanged. `Crossing` never looks past the first crossing,
  so every result is identical by construction; a series that never crosses runs the ladder to
  its end as today.
- Test: `R043_TheLadderStopsOnceEverySeriesHasCrossed` — a flat scene where everything crosses
  early: the exact targets, the visibility target and every row equal the full-ladder run (a
  `Simulate` overload or an internal flag that forces the full ladder, test-only), and the
  number of lifts (exposed as an internal `StepsLifted` on the result, or counted through a
  test hook) is under the ladder's 56.
- Proof: `/validate` dump identical to the baseline; timings.
- Commit: `Stop the simulation's ladder once every series it reads has crossed its level`.

## Step 2. The report's texts (items 1, 2, 4, 5, 6+8, 7), one commit each

**Items 1 and 13 together: the Dmax figure and the flag say what they measure.**
- `SimulationResult.AlreadyPrintSafe` → `DustOnPaperBlackAsIs`; `DmaxToPassAsIs` →
  `DmaxDustOnPaperBlackAsIs` (the Dmax whose paper black is the as-is dust's L\*; NaN at L\* 0).
  Every caller: `Recommendations.Background`, `ReportWriter.ExactLine`, `ValidationTests`
  (keys `simulation.alreadyPrintSafe` → `simulation.dustOnPaperBlackAsIs`,
  `simulation.dmaxToPassAsIs` → `simulation.dmaxDustOnPaperBlackAsIs`: an intended rename of
  the keys in `docs/validation.json`, applied with the owner's approval at the drift step),
  `PrintStretchSimulatorTests`, `BackgroundRecommendationTests`.
- Texts (Romanian, no diacritics), in `Recommendations.Background`'s `Because` and in
  `ReportWriter.ExactLine`, one helper `ReportWriter.DustDmaxText(SimulationResult)` both use:
  `praful ajunge pe negrul hartiei de la Dmax 2.28` / `praful nu ajunge pe negrul hartiei la
  niciun Dmax (e la L* 0)`; when `SkyBelowVisibility`: `; asa cum e, nicio hartie nu face
  imaginea print-safe: cerul e sub L* 15, iar remediul e ridicarea fundalului la stretch`.
- Requirements: R-043 (`Dmax de la care trece asa cum e` → the Dmax from which the as-is dust
  reaches paper black, with the sky sentence), R-092 (the `Because` example).
- Tests: `R043_TheDmaxFigureNamesTheDustAndSaysWhenNoPaperMakesTheImagePrintSafe` on the flat
  0.08 scene (sky under 15: the sentence present) and the flat 0.30 scene (absent).
- Goldens: `dark-sky.txt`, `dark-sky-wcs.txt` (lines 20 and 70/71: new phrase plus the sky
  sentence, sky L\* 7.2), `clean-sky-canvas.txt`, `mono-png8.txt` (lines 18 and 63/64: the
  phrase alone). README line 138 ("Dmax-ul de la care imaginea trece așa cum e") reworded.
- Commit: `Name the Dmax figure for the dust it measures and say when no paper makes the image print-safe`.

**Item 2: no spline rises over 1; Maxima's factor is capped** (`ToneCurve.cs`, `CurveAdvisor.cs`).
- `ToneCurve`: compute the unclamped spline in the table pass and record `IsWithinRange`
  (the unclamped spline never under 0 or over 1 + 1e-9 on (0, 1)); `IsMonotone` judged on the
  unclamped values too (today on the clamped table). A new `IsSuggestible => IsMonotone &&
  IsWithinRange`. The header comment's claim that the clamped table is judged is replaced.
- `CurveAdvisor.Curves`: a local `Cap(double kMax)` returns the largest factor `k <= kMax`
  (bisection, 40 steps, on `Build(Full(k), 1).IsSuggestible`; `k = 0` is the identity and
  always passes) — the owner's rule for Maxima, "the largest factor up to room/reach whose
  spline stays at or under 1". Maxima uses `Cap(room / reach)`, and exists only when its peaks'
  output exceeds Puternica's by more than `CurveMaximaMinGain`. **Assumption to confirm at
  approval:** the same cap is applied to the full curve's factor, `Cap(min(room / reach, 1))`,
  so Puternica never overshoots either, and Blanda and Medie are fractions of a curve that stays
  in range; a built curve that still fails `IsSuggestible` is dropped as today. The alternative
  (check only, no cap on the full curve) would drop Puternica wherever its spline overshoots,
  which V-008 may do (peaks at 0.919 next to (1, 1)); the measurement in this step says which
  images that touches, and the plan's record keeps both figures.
- Measure first, on V-008, V-009, V-011, V-014 (the probe): per suggestion, the maximum of the
  unclamped spline on (0, 1), the capped factor, where the peaks land, the share mapped to white.
- Requirements: R-140 (a spline that is not monotone, or leaves 0..1 between its ends, is never
  suggested; judged on the spline itself, not the clamped table), R-142 (Maxima's factor capped;
  the example figures of V-009 re-measured).
- Tests: `R140_ACurveWhoseSplineLeavesTheRangeIsNotSuggestible` (points (0,0), (0.72,0.92),
  (1,1) overshoot; a clamped-and-kept curve is no longer accepted),
  `R142_MaximaIsCappedSoThatItsSplineStaysUnderOne` (anchors whose uncapped Maxima overshoots:
  the capped factor is under room/reach, the peaks under the target, every suggestion
  `IsSuggestible`, Maxima's gain over Puternica above `CurveMaximaMinGain` or Maxima absent).
  The existing `R142_TheSuggestionsNeverMoveTheDustOrTheSky...` keeps its assertions, with
  Maxima's peaks "at the target" relaxed to "the capped value".
- ADR 0039 "Suggested curves are judged on the unclamped spline, and Maxima's factor is capped
  under the overshoot" — supersedes ADR 0032 in part (decision 4); ADR 0032's header gets the
  supersession line (the kit's ADR 0024). `measurements.md` F1 updated.
- Validation: V-009, V-011, V-014 Maxima figures and `monotone` keys move: intended, with the
  owner's approval at the drift step.
- Commit: `Cap Maxima under the spline's overshoot and judge every suggestion on the unclamped spline`.

**Item 4: `Keep16BitMaster` reads the difference** (`QuantizationAnalyzer.cs`).
- `Keep16BitMaster => Applicable && MissingFrom8 - MissingFrom16 > Thresholds.MissingLevelsWarn`.
- R-082 reworded to the difference, aligned with R-096 ("the levels missing on the 8-bit path
  against the 16-bit one"); `measurements.md` C4; `recommendations-and-report.md` row.
- Test: `R082_AnUpscaledSourceMissesTheSameLevelsOnBothPathsAndKeepsNoMaster` (a 16-bit raster
  of n x 257 values with a ramp: `MissingFrom8 == MissingFrom16 > 20`, flag false); the ramp
  test keeps `true` (`MissingFrom16` 0).
- No figure moves on the reference images (`MissingFrom16` is 0 there); `/validate` confirms.
- Commit: `Keep the 16-bit master only when 8 bits lose levels that 16 bits keep`.

**Item 5: the crop is decided on pixels, and the two sides are named when they differ**
(`CropAnalyzer.cs`, `Recommendations.cs`, `ReportWriter.cs`, `ResultTabs.cs`).
- `Analyze`: compute the new width or height first; `Needed` is true only when it removes at
  least one pixel; `CutPerSidePx` → `CutStartPx` (left / top) and `CutEndPx` (right / bottom,
  one more on an odd cut). Validation keys: `crop.cutPerSidePx` → `crop.cutStartPx`, plus
  `crop.cutEndPx` (owner's approval at the drift step; V-002 guards 192 / 192).
- One helper `ReportWriter.CropCutText(CropResult)`: `192 px pe fiecare parte din latime` when
  equal, else `0 px la stanga si 1 px la dreapta din latime` / `... sus si ... jos din inaltime`;
  `Recommendations.Size` and the Preview tile's `Note` use it (the window formats, the core
  decides the words).
- R-032 reworded; `measurements.md` A3; `window.md` if it names the note.
- Tests: `R032_ACropIsNeededOnlyWhenAPixelIsRemoved` (4961 x 3508 at 297:210: not needed;
  3508 x 2480 at 297:210: needed, 0 and 1 px, the text names both sides); existing crop tests
  renamed `R032_...`.
- Goldens: `dark-sky.txt` runs `--aspect 50:35` on 512 x 512 (cut 55 px per side, equal):
  the sentence changes only if its wording changes; read the diff.
- Commit: `Decide the crop on the pixels it removes and name both sides when they differ`.

**Items 6 and 8: the target background in one place** (`Parameters.cs`, `Options.cs`,
`ParameterFields.cs`, `AnalyzerCli.cs` help, README).
- `Parameters`: `TargetBackground`'s `init` rounds to 3 decimals (`Math.Round(v, 3)`); a static
  `TargetBackgroundError(double given)` returns the one Romanian message
  (`fundalul-tinta trebuie sa fie un numar strict intre 0 si 1 (se rotunjeste la 3 zecimale); a
  primit {given}`) for NaN, infinity, or a rounded value not strictly inside (0, 1), else null;
  `Validate()` calls it on the stored value. `Options.Parse` (`--target-bg`: the core's message
  prefixed `--target-bg: `) and `ParameterFields.TryBuild` (the field: the core's message
  instead of `OptionalNumber(…, 0, 1)`'s `(0.0, 1.0]`) call it, so both refuse the same values
  with the same words before any reading. `PrintStretchSimulator.Simulate` compares the row's
  target with the rounded given one (like with like): a 7-decimal input keeps `(dat)` and the
  Preview role (item 8); its own `Math.Round(…, 6)` on the ladder steps stays.
- R-023 (rounded to 3 decimals; refused outside (0, 1), NaN, infinity, with the message), R-003
  and `Golden/help.txt` (`--target-bg <0..1>` line: "strict intre 0 si 1, 3 zecimale"), R-026
  (`TargetBgHelp` says the interval), `console.md` "Parameters" row, README lines 154 and the
  window paragraph.
- Tests: `R023_TheTargetBackgroundIsRoundedToThreeDecimalsAndRefusedOutsideTheOpenInterval`
  (`Parameters`: 0.1234567 → 0.123 and `Explicit` on its row, Primary; 0.0004, 0.9996, NaN,
  +inf refused with the message; `Options`: `--target-bg 1`, `0`, `NaN`, `0.0004` are errors
  naming `--target-bg`; `ParameterFields`: the same values give the core's message), the
  existing `R023_AnExplicitTargetJoinsTheGridAndIsThePrimaryRow` extended with the 7-decimal
  case.
- Goldens: `help.txt`; `dark-sky-wcs.txt` runs `--target-bg 0.135` (3 decimals: unchanged).
- ADR 0040 "The target background is rounded to three decimals and must lie strictly inside
  (0, 1); the exact targets are read on the regular ladder only" (items 6, 7, 8 together).
- Commit: `Round the target background to three decimals and refuse it outside (0, 1) in one place`.

**Item 7: the exact targets are read on the regular ladder only** (`PrintStretchSimulator.cs`).
- A step feeds the dust, sky and cloud series only when its target lies within
  `[ExactTargetLo, ExactTargetHi]` (1e-9 tolerance): the regular steps, the grid rows inside it
  and a given target inside it (as today). A given target outside it is lifted for its table
  row only. `CloudRow.ExactTarget` and `SimulationResult.ExactTarget` doc comments drop the
  "or at a given target above it" clause; `Crossing` returning null then prints `> 0.60` as
  R-043 says. Interacts with 15a: the early exit's "rows left" count includes the out-of-range
  row.
- R-043 (the ladder's bounds are the ladder's whatever the given target).
- Test: `R043_AGivenTargetOutsideTheLadderGetsItsRowButDoesNotFeedTheExactTargets` (a dark
  flat scene whose dust reaches black only above 0.60 with `TargetBackground = 0.70`: the row
  exists and is Explicit, `ExactTarget` null, `TargetText` "> 0.60"; a target 0.03: row exists,
  `ExactTarget` not 0.03).
- Commit: `Read the exact targets on the regular ladder only, whatever target was given`.

## Step 3. The measurements' precision (items 3, 10, 12, 13, 14, then 9)

**Item 3: the dark shares count 8-bit levels** (`TonalAnalyzer.cs`).
- Count `RasterOps.Level8(v) < DarkLevelA` (and B) on every format. For speed on 69 M samples
  the loop uses the exactly equivalent test `v * 255.0 < DarkLevelA - 0.5` in double
  (`Math.Round` is banker's: 31.5 → 32, and 32 and 64 are even, so `round(x) < N ⟺ x < N -
  0.5`); a test proves the equivalence against `Level8` on all 65 536 16-bit levels and random
  floats. `TonalResult.PctBelowDarkA/B` comments and the `Analyze` comment say "the 8-bit level
  an export would write"; `ReportWriter`/window labels "sub nivelul 32" are now true.
- R-040 reworded; `measurements.md` B1.
- Tests: `R040_TheDarkSharesCountTheEightBitLevelAnExportWouldWrite` (an 8-bit-style raster at
  n x 257 / 65535 for n = 31, 32, 33: 100 / 0 / 0 % under 32; a 16-bit raster at 31.4 and
  31.6 / 255: under and not; equivalence with `Level8`); existing tonal tests renamed `R040_`.
- Goldens: `dark-sky.txt` (16-bit), `clean-sky-canvas.txt`, `mono-png8.txt` (8-bit) "sub
  nivelul 32 / 64" lines may move by tenths: read and report each.
- Validation: `tonal.pctBelowDarkA/B` move a little on the 16-bit and float cases: intended
  (owner's approval at the drift step).
- Commit: `Count the dark shares on the 8-bit level an export writes, on every format`.

**Item 10: the clouds are found by angular distance alone** (`DustAnalyzer.FindClouds`).
- Drop the pixel bounding box. Pre-filter per cloud on the frame's centre: the unit vector of
  the frame centre (`wcs.PixelToSky` of the image centre) and the frame's angular radius
  (the largest angle from it to any patch centre, from the dot products already computed); a
  cloud is tested only when its centre is within `frameRadius + cloud radius` of the frame
  centre (cosine compare). Then every patch centre is tested by dot product (`>= cos r`).
  `CentreX/Y/RadiusPx` still come from `SkyToPixel`; a centre that does not project (NaN) is
  skipped with a comment that the pre-filter already excludes it on any frame narrower than
  180 degrees. `DustCloud.Patches` comment: every patch whose centre lies in the disc.
- Time the map build on the plate-solved FITS before and after (the probe; expected: tens of
  ms for 1787 clouds x 21 840 patches) and record it.
- R-044 already says "whose disc holds a patch"; `measurements.md` B5 (FindClouds) updated.
- Test: `R044_ACloudFarFromTheTangentPointKeepsEveryPatchWhoseCentreLiesInItsDisc` (a wide
  synthetic WCS with a cloud near the frame's edge whose projected disc stretches past the old
  box: patch count equals the brute-force angular count; a cloud with patches inside the frame
  but its box outside is found).
- V-004: `cloudCount` 397, 114 rows and every cloud figure should not move; `/validate` says.
- ADR 0044 "Clouds are found by angular distance alone, without a pixel bounding box".
- Commit: `Find the catalogued clouds by angular distance alone, without a pixel box`.

**Item 12: `StarAnalyzer` reads `Thresholds.StarWindow`** (`StarAnalyzer.cs`).
- `const int Half = Thresholds.StarWindow / 2;` the border buffer, the border array and the
  line length derive from it; the comments name the threshold (`<see cref="Thresholds.StarWindow"/>`).
- Test: `R070_TheWindowIsTheThresholdsStarWindow` (two equal stars `Half` px apart: one kept;
  `Half + 1` apart: two; and the border of `Half` px holds no star).
- No figure moves. Commit with item 14 (same file, same requirement row).

**Item 14: tied maxima count once** (`StarAnalyzer.IsWindowMax`).
- Ties stay allowed, but a pixel equal to the candidate that comes earlier in scan order (a row
  above, or the same row to the left) disqualifies it: a flat two-pixel peak is measured once,
  at its first pixel.
- R-070: "the maximum, ties allowed, of its 25 x 25 window ... among equal maxima only the first
  in scan order is counted, so a flat peak is counted once"; `measurements.md` D1; ADR 0016 gets
  the dated supersession note and ADR 0041 "Among tied window maxima only the first in scan
  order is a star" supersedes it in part.
- Test: `R070_AFlatTwoPixelPeakIsMeasuredOnce` (a synthetic star whose two central pixels are
  set equal: one star, at the first pixel; the field test's count unchanged).
- Validation: `stars.*` may move a little on every case: intended.
- Commit: `Read the star window from Thresholds and count a flat peak once`.

**Item 9: the gamut figure, measure first** (`GamutAnalyzer.cs`, `Thresholds.cs`).
- Probe (scratchpad, a copy of the review's `probe` project): on the plate-solved FITS with
  both lab profiles and on `de_printat.png`, on the tonal subsample and the map, as is and after
  `Puternica`: (0) the exact per-pixel round trip (the reference: PctAll and the five classes;
  about 1.4 MP x 8-28 microseconds per measure, minutes in all); (a) the grid of 65^3 (build
  time and figures); (b) the hybrid: the grid of 33 (or 65), plus the exact round trip for the
  pixels whose interpolated displacement lies within a margin of `CurveGamutDeltaAb` (margins
  0.5, 1.0, 1.5; the count of such pixels, the time, the figures); (b') the hybrid with the
  exact trips cached per 8-bit colour, if (b) is too slow. Per "Calculeaza" the cost is one
  build plus up to five measures.
- Decision rule (the owner's): implement the cheapest option that brings every class within
  about 0.2 points of the exact figure if it adds no more than about 1 s per "Calculeaza";
  otherwise stop and present the measurements to the owner (no code change for item 9 then).
- If implemented: the new constant(s) in `Thresholds.cs` with their comment (a margin, a grid);
  R-144 ("evaluated once on a grid ... and interpolated" reworded), `measurements.md` F2 and the
  header comment of `GamutAnalyzer.cs` (the undercount sentence replaced by the new figures),
  `thresholds.md` row; `known-limitations.md` if a residual error stays; ADR 0042 "The gamut
  figure is ..." with the measurements; a `R144_` test on a synthetic profile (the existing
  `R144_TheGamutTableIsInterpolatedAndCountedPerClassOfThePatch` adapted). V-008's gamut
  figures move: intended.
- Commit: `Measure the gamut figure within 0.2 points of the exact round trip` (or none).

## Step 4. Documentation (item 11), decisions, CHANGELOG, README

- Item 11: a line in `docs/design/known-limitations.md`: on 8-bit files the background levels
  are whole levels, so a cast near 1 level depends on the tie rule of the darkest-quarter cut
  (`<=` or `<`: +2.0 against +1.0 levels on the reference image's 8-bit version); the code stays
  as ADR 0014 decided. Also the stale gamut sentence there, if item 9 changed the method.
- ADRs 0039 (curves), 0040 (target background and the ladder), 0041 (stars), 0042 (gamut, if
  implemented), 0043 "The report's figures say what they measure: the dust's Dmax, the levels
  an export writes, the master's missing-level difference, the pixels a crop removes" (items 1,
  3, 4, 5, 13), 0044 (clouds); each quotes the owner's answer of 2026-10-23 and names its tests.
  ADR 0016 and ADR 0032 get their supersession lines (`scripts/check-decisions.ps1` checks both
  sides).
- `docs/requirements.md`: R-023, R-032, R-040, R-043, R-070, R-082, R-092, R-096 (unchanged
  wording, checked), R-140, R-142, R-144; statuses `implemented` where the `R###_` tests exist.
- Design notes: `measurements.md` (A3, B1, B4 with the early exit, B5 FindClouds, D1, F1, F2,
  the phase-2 note on `<=`), `thresholds.md` (the gamut row's stale 0.01 %, any new constant),
  `console.md` (the target-background row), `recommendations-and-report.md` (Keep16BitMaster
  row, crop and Dmax texts), `window.md` (the crop note, if named), `input.md` (Stats by
  selection), `known-limitations.md`.
- `README.md` (Romanian with diacritics): the Anexa B sentence on the Dmax (line 138),
  `--target-bg` (line 154) and the field paragraph (interval, 3 decimals), the crop sentence if
  one names "pe fiecare parte".
- `CHANGELOG.md` under `[Unreleased]`: Fixed, one line per user-visible fix (1, 2, 3, 4, 5, 6+8,
  7, 10, 14, 9 if done); Internal for 15 and 12.
- The plan's "Record" section: timings, the measurements of items 2, 9 and 10, the golden diffs
  with each changed line's cause, the drift table.

## Step 5. Validate, goldens, wrap-up

1. `/validate` in the worktree: the drift table with every moved figure tied to its fix (items
   2, 3, 14, 9, the renamed keys of 1, 5 and 13); V-004 not moving (item 10), V-001/V-002/V-005
   keep16BitMaster not moving (item 4). **Ask the owner before editing `docs/validation.json`**;
   then update it, with dated History lines in `docs/validation.md`.
2. Goldens regenerated with `APA_UPDATE_GOLDEN=1` only after the text fixes; the whole diff read
   and every changed line reported with its cause (the kit's ADR 0033).
3. `/wrap-up`: `scripts/verify.ps1` green, CHANGELOG, requirements, decisions, the plan saved
   under `docs/plans/`, commits on the task branch; propose the merge into `main` (the owner
   decides) and suggest `/code-review main...claude/Fix-the-defects-the-analyzer-review-found`.

## Verification

- Gate: `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify.ps1` (kit files,
  manifest, decisions, design index and Covers paths, restore, format, build with warnings as
  errors and the XML doc file, tests without Validation).
- `/validate` after step 1 (identical dumps) and at the end (drift table).
- Window: `APA_DUMP_SCREENS=<dir> dotnet test AstroPhotoAnalyzer.Wpf.Tests --filter WindowSmokeTests`
  for the crop note and the target-background field's message.
- Console on the reference images (`dotnet run --project AstroPhotoAnalyzer -- <image> ...`) for
  the new Anexa B sentence and the timings.

## Assumptions stated

1. Item 2: the cap applies to the full curve's factor too (Puternica), not to Maxima alone; the
   measurement records what the check alone would have dropped.
2. Item 7: "outside it" means outside `[ExactTargetLo, ExactTargetHi]`; a given target inside the
   range still feeds the series, as today.
3. Items 1, 5, 13: the validation keys are renamed with the properties (`dustOnPaperBlackAsIs`,
   `dmaxDustOnPaperBlackAsIs`, `cutStartPx` + `cutEndPx`), applied only with the owner's
   approval of the drift table.
4. Item 15b: `Stats.Median` itself becomes selection-based, so every caller benefits with
   identical results; `DustAnalyzer.Measure`'s class-set sorts stay (among "the smaller ones").

## Record

### Step 0: baseline (2026-10-23)

The seven reference files copied into the worktree, `scripts/check-testdata.ps1` all `ok`.
`APA_VALIDATION_DUMP` on `main` (680951d): 14 cases, 1689 figures (`baseline.jsonl`, the
scratchpad). Console in Release, `bash time`, three runs each: `de_printat.png` 12.55 / 11.83 /
12.17 s (median 12.17); `starless_rbf0.5_stretch0.17.fit` 14.80 / 15.33 / 14.65 s (median
14.80).

### Step 1, 15b: selection instead of the full sorts

`/validate` dump after the change: 14 cases, 1689 figures, 0 moved against the baseline
(`drift.py`, every figure compared as the test rounds it). Console: `de_printat.png` 10.99 /
10.88 / 11.14 s (median 10.99, 1.2 s less); the FITS 14.10 / 13.86 / 14.31 s (median 14.10,
0.7 s less). The unit tests: 352 + 54 green.

### Step 1, 15a: the ladder stops early

`/validate` dump after the change: 14 cases, 1689 figures, 0 moved against the baseline.
Console: `de_printat.png` 9.27 / 9.22 / 9.64 s (median 9.27, 1.7 s less than after 15b, 2.9 s
less than the baseline); the FITS 12.24 / 12.47 / 12.39 s (median 12.39, 1.7 s less than after
15b, 2.4 s less than the baseline). Steps lifted (`SimulationResult.StepsLifted`, the probe
`fixprobe steps`), of the ladder's 56: `de_printat.png` 16 at Dmax 2.0 and 2.4 (the last grid
row, 0.20, ends it); the plate-solved FITS 35 at Dmax 2.0 and 25 at 2.4 (the darkest cloud row
crosses last); Rosetta, Veil and Orion 16 at both. `Simulate` alone: 1.85 s on the PNG, 2.29 s
on the FITS at Dmax 2.0 (2.75 and 2.6 s recorded by ADR 0035).

### Step 2, items 1 and 13: the Dmax figure and the dust flag (goldens)

Eight lines in four goldens, all the one sentence this fix means to change: in each of
`dark-sky.txt`, `dark-sky-wcs.txt`, `clean-sky-canvas.txt` and `mono-png8.txt` the `fiindca:` of
the background section and the `tinta exacta` line of Anexa B. `trece asa cum e de la Dmax X`
became `praful ajunge pe negrul hartiei de la Dmax X` (X unchanged: 2.28, 2.28, 1.3, 1.3); the
two dark scenes (as-is sky L\* 7.2) add `; asa cum e, nicio hartie nu face imaginea
print-safe: cerul e sub L* 15, iar remediul e ridicarea fundalului la stretch`, the two bright
ones (sky L\* 27) do not. No other line moved.

### Step 2, item 2: the spline's overshoot (the probe `fixprobe maxima`, before the change)

| image | anchors (dust, sky, band, clouds, peaks) | room / reach | Puternica: peaks y, raw max | uncapped Maxima: peaks y, raw max, monotone, input mapped to white | capped: factor, peaks y, gain over Puternica |
| --- | --- | --- | --- | --- | --- |
| plate-solved FITS (V-008) | 0.0914 0.1690 0.3057 0.5431 0.7199 | 1.0014 | 0.9189, 1.00000 | 0.9200, 1.00000, yes, 0 % | 1.0014, 0.9200, 0.0011 (no Maxima, under 0.02) |
| de_printat.png (V-009) | 0.0574 0.0989 0.2160 0.3945 0.5932 | 1.2633 | 0.7489, 1.00000 | 0.9200, 1.00998, no, 24.2 % | 1.2198, 0.8917, 0.1428 |
| Rosetta (V-011) | 0.0769 0.0929 0.2723 0.4194 0.6025 | 1.1904 | 0.7877, 1.00000 | 0.9200, 1.00657, no, 21.2 % | 1.1605, 0.8992, 0.1115 |
| Veil (V-014) | 0.0779 0.1023 0.1662 0.3585 0.4980 | 1.5594 | 0.6267, 1.00000 | 0.9200, 1.05064, no, 41.0 % | 1.3833, 0.8277, 0.2010 |

On every image Blanda, Medie and Puternica have a raw spline maximum of exactly 1.00000 (at the
(1, 1) end) and are monotone: the check alone would have dropped nothing, and the cap on the
full curve's factor (assumption 1 of the plan) changes nothing; it stays as the one rule. The
raw minimum is 0.00002 everywhere (the dust's point, above 0). Found on the way: the curve
(0, 0), (0.5, 0.98), (0.6, 0.99), (1, 1) of the old R-140 test stays within 0..1 (its comment
said it passed 1) but is not monotone; the test now says so.

### Step 2, items 4, 5, 6 and 8 (goldens)

Item 4 and item 5 moved no golden line (the stress test of the dark scene misses no level from
16 bits, and its crop of 77 px per side keeps the equal-sides wording). Items 6 and 8 moved one
line, in `help.txt`: the `--target-bg` line gained ` (strict intre 0 si 1, 3 zecimale)`, the
intended change of the help text. `dark-sky-wcs.txt`, which runs `--target-bg 0.135`, did not
move: three decimals round to themselves.

### Step 3, item 3: the dark shares (goldens)

Four lines, one per golden, all the Anexa A line `sub nivelul 32: ... sub 64: ...`, the one
figure this fix changes: `dark-sky.txt` and `dark-sky-wcs.txt` (16-bit, the same scene) 81.0 %
to 80.6 % and 92.4 % to 92.3 % (the samples between 31.5 and 32 levels, and between 63.5 and
64, no longer count); `clean-sky-canvas.txt` (16-bit, a flat 0.25 = level 63.75 with 1 LSB of
noise) `sub 64` 59.3 % to 39.7 %, and `mono-png8.txt` (8-bit, the same level) 76.7 % to
39.6 %: both scenes sit on the threshold itself, so "under 64 as a float" (59 %) or "at or under
the level 64" (77 %, the 8-bit tie the review found) become "rounds to a level under 64" (40 %),
the share an 8-bit export would show. `sub nivelul 32` stays 0.0 % on both.

### Step 3, item 10: the clouds without a box (the probe `fixprobe clouds`)

On the plate-solved FITS: `DustAnalyzer.Build` with the WCS 156 ms before the change, 174 ms
after (8 and 10 ms without a WCS); a brute force over 1787 clouds x 21 840 patches 49 ms before,
66 ms after (the same code, the run's noise); 397 clouds, 114 rows and 5110 patches by both the
old box and the brute force at this pointing, so V-004 should not move. The regression test
points the FITS's geometry 30 degrees east of LDN 621 (radius 3.78 degrees): the new search
agrees with brute force on every cloud, and LDN 621 has patches outside the old box (the test
prints their count on failure; the first draft of the test, on a 512 px frame of 0.15 degrees
per pixel, found 46 clouds and none outside the box: its 4.8-degree patches were too coarse to
show the loss).
