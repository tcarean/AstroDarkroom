# Name the trail axis per frame, show the run at 10 px per frame, explain the rejected list

Branch `claude/Name-the-trail-axis-and-the-repeated-spot-per-frame`, worktree
`.claude/worktrees/Name-the-trail-axis-and-the-repeated-spot-per-frame`. Requirements R-033,
R-034, R-035 (draft, accepted in chat on 2026-10-09); ADR 0011 (proposed; this plan's approval
makes it accepted). The failing tests are written: `R033_*` in `SeriesTests`, `SeriesAnalyzerTests`,
`R034_*` in `DitheringTests`, `SeriesTests`, `R035_*` in `MainViewModelTests`; V-009 in
`docs/validation.json` with `expected: null`.

## Context

The owner read `alungire 1.76 (prag 1.60), dara la -90 grade` in RESPINSE and could not tell
what it meant nor on which mount axis the trail was. The series already knows the RA axis
(R-025) and each frame its trail angle and coherence (R-013); nothing joins them per frame.
The owner also wanted to see which frames sit in a run without an effective dither (the walking
noise), without rejecting them: measured on Veil, rejecting the 20 frames beyond the third of
a 10 px run would cut 30 % of the signal and leave runs of 3 (ADR 0011). Outcome: the reason
names the axis (`dara pe RA (-90 grade)`), the Respinse tab explains the words, the frame table
gets a cell `k/n de la f` for the run at 10 px, the map rings those frames. No threshold is new.

## Steps

### 1. Core, `Analysis/` (numbers)

`src/LightsCheck.Core/Analysis/SeriesAnalyzer.cs`
- `internal enum TrailAxis { Unknown, Ra, Dec, Oblique }`.
- `FrameFlag` gains `TrailAxis On = TrailAxis.Unknown` (positional record, last parameter).
- `SeriesAnalysis` gains `TrailAxis TrailOn` right after `TrailAngleDeg`.
- `internal static TrailAxis TrailAxisOf(double angleDeg, Axis ra)`: `Unknown` when `ra` is
  unknown; distance from vertical `|90 - |a||`, from horizontal `|a|`; the RA axis's distance
  under `Th.TrailAxisTolerance` gives `Ra`, the other axis's `Dec`, else `Oblique`.
- In `Analyze`, first pass: the `MarginalTrail` flag gets the angle as `Detail`, the same
  expression the `Trailed` flag uses (extract a local `string? angle`).
- After `ra` is decided (the block "The RA axis"): for every frame whose flag list holds a
  `Trailed` or `MarginalTrail` flag, when `ra != Unknown`, `m.AngleDeg` and `m.AngleCoherence`
  are set and the coherence exceeds `Th.TrailAxisCoherence`, replace that flag with
  `flag with { On = TrailAxisOf(angle, ra) }`. `TrailOn = trailAngle is double t ? TrailAxisOf(t, ra) : Unknown`
  and passed to the `SeriesAnalysis` constructor.

`src/LightsCheck.Core/Analysis/Dithering.cs`
- `internal sealed record SpotRun(int Ordinal, int Frames, int FirstIndex)` next to `FrameStretch`.
- `DitherAnalysis` gains `IReadOnlyList<SpotRun?> SpotRun` after `RunsLoose`.
- In `Analyze`, after `runsLoose`: an array of `n` nulls; for each run, for `k` in `0..Frames-1`,
  `spot[run.FirstIndex - 1 + k] = new(k + 1, run.Frames, run.FirstIndex)`. The runs already
  exclude unregistered frames and stretches under `Th.RunMinFrames`.

### 2. Core, `Report/` (words)

`src/LightsCheck.Core/Report/SeriesReport.cs`
- `Reason`: `Trailed` and `MarginalTrail` end with `TrailSuffix(f)`: `", dara pe RA (-90 grade)"`
  (`AxisWord`: `pe RA`, `pe DEC`, `oblica`) when `On` is known, `", dara la -90 grade"` when
  only `Detail` is set, nothing otherwise.
- `TrailRows`, row `directia darelor`: after `... pe cadrele slabe`, append `" = " + AxisWord(s.TrailOn)`
  when known.
- `FrameTableRow` gains `string Spot` after `Dy`; `FrameRows` fills it from `d.SpotRun[i]`:
  `"{Ordinal}/{Frames} de la {FirstIndex}"` or `"-"`.
- The table header becomes `...     dX      dY  acelasi loc     motive` (the new cell `{f.Spot,-14}`
  between `{f.Dy,6}` and the reasons, two spaces each side, left-aligned).
- `internal const string RejectedExplanation`: five sentences in Romanian without diacritics,
  like the rest of the report: what the elongation is and the two thresholds; where the marginal
  frames are (`la limita` row of CADRE MISCATE in Rezumat, the class in Cadre); what the trail
  and its angle are; `pe RA` = tracking (periodic error, speed, RA backlash), `pe DEC` = polar
  alignment, wind, a touched mount, `oblica` = both or a knock; the axis word needs a known RA
  axis and stars that agree (the coherence).

`src/LightsCheck.Core/Report/SeriesOutputs.cs`
- CSV header: `trail_on` after `coherence`; `spot_run_index,spot_run_frames,spot_run_first`
  after `nearest_other_px`. Cells: the frame's `Trailed`/`MarginalTrail` flag's `On` as `RA`,
  `DEC`, `oblic` or empty; the three run figures or three empty cells.

### 3. Window (binds, computes nothing)

- `src/LightsCheck.Wpf/Presentation/Charts.cs`: `ChartPoint` gains `bool Ring = false`;
  `DitherMap.OnRender` draws a ringed point as `DrawEllipse(null, FrozenPen(p.Colour, 1.5), here, 4, 4)`
  instead of the filled dot (Charts.cs is LightsCheck's own, not a FlatCheck copy).
- `src/LightsCheck.Wpf/ViewModels/MainViewModel.cs`: `FrameRowVm.Spot` from `row.Spot`;
  `public string RejectedExplanation => SeriesReport.RejectedExplanation`; the map point gets
  `Ring: d.SpotRun[i] is not null`.
- `src/LightsCheck.Wpf/Views/FramesView.xaml`: a column 12 (`Width="100"`) in both grids
  (header and row): header `acelasi loc`, tooltip "al catelea cadru consecutiv in acelasi loc
  (10 px), din cate, de la care cadru"; `motive` moves to column 13.
- `src/LightsCheck.Wpf/Views/RejectedView.xaml`: a `Muted` wrapped `TextBlock` bound to
  `RejectedExplanation` under the heading row, above the "Scrie fisierele" note.
- `src/LightsCheck.Wpf/Views/DitherView.xaml`: one `Muted` line under the map: "inel = cadru
  dintr-un sir de 3 sau mai multe cadre consecutive in acelasi loc (10 px)".

### 4. Tests and goldens

- `tests/LightsCheck.Tests/ValidationTests.cs`: a `case "words"` in the switch: when the
  expected element is `Null`, print `{id}: unmeasured` and return; else `CheckWords`: every bad
  frame's and every marginal frame's trailing flag `On` word equals `badTrailOn` /
  `marginalTrailOn`, and `spotCells` (an object `{"60": "...", "71": "-"}`) equals
  `SeriesReport.FrameRows(r)[index - 1].Spot`.
- Regenerate `series.txt` and `series-classes.txt` with `LIGHTSCHECK_UPDATE_GOLDEN=1`
  `--filter GoldenOutputTests`; read the whole diff. Expected lines: RESPINSE and table reasons
  of frames 3 and 6 (`dara pe RA (-90 grade)`), the `directia darelor` row (`= pe RA`), the
  table header and every table row (the new `-` column) in `series.txt`; in `series-classes.txt`
  the table header and rows, and frame 3's marginal reason gaining `, dara la 90 grade` (the RA
  axis there is unknown, so no axis word on 3 or 7). Any other changed line is a finding.

### 5. Measure (step 7 of /new-feature)

- `dotnet run --project src/LightsCheck.Cli -c Release -- <Veil session> --out <scratchpad>`
  (about 90 s on 4 threads); read `trail_on` and the `spot_run_*` columns of the CSV for the
  bad, the marginal and frames 60 to 71; fill V-009's `expected` (`badTrailOn`, `marginalTrailOn`,
  `spotCells`) and `measuredOn`, paste the figures in chat, update the V-009 row of
  `docs/validation.md`; fix the R-034 example if frame 71 is not `-`.
- `Analysis/` and `Report/` are numeric code (the rule "Measured on the sessions"): run
  `/validate` (V-001 to V-009) and paste the drift table. No constant changed, so V-001 to V-008
  must not move.

### 6. Record

- `docs/requirements.md`: R-033, R-034, R-035 to `implemented`.
- `docs/decisions/0011-...md`: `Status: accepted 2026-10-09`.
- `docs/design/series-analysis.md`: the per-frame axis (derived from the series' axis after it
  is decided, gated by the vote's coherence), `SpotRun`, the new cell and CSV columns, the
  explanation text; `docs/design/window.md`: `Spot`, `Ring`, `RejectedExplanation`.
- `README.md`: the CADRE MISCATE bullet (the axis word), the `alungire` row of the table
  legend, a `acelasi loc` line, the Cadre, Dithering (the ring) and Respinse (the explanation)
  tab bullets, the CSV columns.
- `CHANGELOG.md` under `[Unreleased]`: Added (the axis word in the reasons and the CSV, the run
  cell and the ring, the Respinse explanation); Changed (the marginal reason carries the angle,
  the table has a new column).
- Save this plan as `docs/plans/2026-10-09-name-the-trail-axis-and-the-run-cell-per-frame.md`.
- `/wrap-up`: gate, diff read back, commit on the branch, merge proposal.

## Verification

1. `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify.ps1 -SkipTests` green
   while editing; the full gate before the commit.
2. `dotnet test LightsCheck.sln -c Release --filter "FullyQualifiedName~R033|FullyQualifiedName~R034|FullyQualifiedName~R035"`
   green (they fail to compile now).
3. The golden diffs read line by line, each tied to steps 1 and 2.
4. `LIGHTSCHECK_DUMP_SCREENS=<scratchpad>` with `WindowSmokeTests`: look at `tab-rejected.png`,
   `tab-frames.png` and `tab-dither.png` to see the explanation, the column and the rings.
5. The Veil run of step 5, with the figures pasted.

## Outcome (2026-10-09)

Followed as written, with three things to note. `R028_TheOutputsAreWrittenWhereAskedAndNeverIntoLights`
pinned the CSV at 57 columns; the four columns R-033 and R-034 add make it 61, and the count in
the test moved with a comment saying why. In `series-classes.txt` the marginal frame 3 gained
`, dara la -90 grade`, not `90`: its measured angle is -90 (frame 7's is 90; both are vertical,
the sign is the edge of the (-90, 90] range). On Veil, frame 60 sits in the run 57 to 60
(`4/4 de la 57`), so V-009 pins frames 57, 60, 61, 70, 71 and 72; the R-034 example (frame 71
reads `-`) held.
