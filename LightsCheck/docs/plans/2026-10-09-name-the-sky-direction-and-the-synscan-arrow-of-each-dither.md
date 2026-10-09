# Name the sky direction and the SynScan arrow of each dither

Branch `claude/Name-the-sky-direction-and-the-SynScan-arrow-of-each-dither`, its worktree under
`.claude/worktrees/`. Requirements R-038 (core) and R-039 (window), drafted; ADR 0013
(proposed; this plan's approval makes it accepted). The failing tests are written: `R038_*` in
`SeriesAnalyzerTests` and `SeriesTests`, `R039_*` in `MainViewModelTests`; V-011 in
`docs/validation.json` with `expected: null`.

## Context

The owner wanted to see, per frame, in which direction the mount was moved for the dither and
which SynScan arrow that was, plus the same text as a tooltip on the step bars. Measured on
2026-10-09 with two `siril-cli` plate solves of raw lights (ADR 0013): the WCS of Siril's
solved FITS applies to the stars' shift with Y flipped, because Siril stores rows bottom-up;
in LightsCheck's pixels `+X` is north and `+Y` is west on Veil. The arrow words follow the
SynScan default keys the owner pasted (left east, right west, up north, down south); no input,
no settings file (the owner's earlier option A is superseded by the pasted default).

## Steps

### 1. Core, `Analysis/`

- New `src/LightsCheck.Core/Analysis/Wcs.cs`: `internal sealed record Wcs(double Cdelt1,
  double Cdelt2, double Pc11, double Pc12, double Pc21, double Pc22)` with `Axis RaAxis`
  (`|Pc12| > |Pc11|` → Y, else X, the rule `SeriesRun.WcsRaAxis` has today) and
  `(double RaDeg, double DecDeg) SkyOffsetDeg(double dx, double dy)` = `(Cdelt1 * (Pc11 dx +
  Pc12 dy), Cdelt2 * (Pc21 dx + Pc22 dy))`, in the solved frame's own pixels; `double ScaleDeg
  => |Cdelt1|`.
- `src/LightsCheck.Core/Runs/SeriesRun.cs`: `WcsRaAxis(root)` becomes `ReadWcs(root)` →
  `Wcs?` (same file scan, same `CTYPE1` and `PC`/`CD` fallbacks, plus `CDELT1`/`CDELT2`; a
  `CD` matrix without `CDELT` means `CDELT = 1`), and `WcsRaAxis(root) => ReadWcs(root)?.RaAxis
  ?? Axis.Unknown` stays for R-025's test. `Run` passes the `Wcs?` to the analyzer.
- `src/LightsCheck.Core/Analysis/SeriesAnalyzer.cs`: `internal sealed record MountMove(double
  RaPx, double DecPx, bool Signed)`; `SeriesAnalysis` gains `IReadOnlyList<MountMove?>
  MountMoves` (after `TrailOn`); `Analyze(..., Wcs? wcs)` replaces the `Axis wcsRaAxis`
  parameter (`ra = wcs?.RaAxis ?? Axis.Unknown` at the top of the RA block). After the RA axis
  is decided, per frame with `dither.IsMove[i]`: with a WCS, `(raDeg, decDeg) =
  wcs.SkyOffsetDeg(-StepX, +StepY)` (the pointing moved by minus the stars' shift, and Siril's
  rows are flipped, ADR 0013) → `MountMove(raDeg / scale, decDeg / scale, true)`; without a WCS
  but with `ra` known, `MountMove(|step along the RA axis|, |step along the other|, false)`;
  else null. Non-moves null.

### 2. Core, `Report/`

- `SeriesReport.cs`: `MoveCell(MountMove?)`: `-` for null; components over `Th.AxisStep` only;
  signed: `RA {|RaPx| F0} px {E|V} ({stanga|dreapta})`, `DEC {|DecPx| F0} px {N|S} ({sus|jos})`,
  joined by `, `; unsigned: `RA 15 px, DEC 12 px`; both components under the bar (cannot
  happen for a move of 5 px or more, but) → `-`. `FrameTableRow` gains `Move` after `Dy` and
  `string? StepTip` (`cadrul {Index}: pas {Step} px; {Move}` when the step exists, else null);
  the console table gets the column `dithering` (width 38, left-aligned) between `dY` and
  `acelasi loc`.
- `SeriesOutputs.cs`: CSV columns `move_ra_px`, `move_dec_px`, `move_arrows` after
  `spot_run_first` (signed figures with `R` formatting when `Signed`, else empty; the arrows
  the words in the cell, `dreapta, jos`, empty otherwise).

### 3. Window

- `Presentation/Charts.cs`: `ChartPoint` gains `string? Tip = null`; `SeriesChart` keeps the
  last layout's `xMin`, `xMax`, `plotW` in fields set in `OnRender`, overrides `OnMouseMove`
  to find the point whose bar (bars) or x (lines) is nearest the cursor within half a slot
  and sets `ToolTip` to its `Tip` (null clears), with `ToolTipService.InitialShowDelay` 200
  set once in the constructor; `OnMouseLeave` clears it.
- `ViewModels/MainViewModel.cs`: `FrameRowVm.Move`; the step point gets `Tip: table[i].StepTip`.
- `Views/FramesView.xaml`: a column `dithering` (width 230) between `dY` and `acelasi loc` in
  both grids, with the tooltip "miscarea monturii la dithering: pe ce axa, cati pixeli, in ce
  sens, si sageata SynScan"; `acelasi loc` and `motive` move one column right.

### 4. Tests, goldens, validation

- `ValidationTests.cs`: `case "moves"`: null → run the series and print the `dithering` cell of
  every move with its frame and the counts of `S`/`N`/`E`/`V` components, return; else
  `CheckMoves`: `cells` (frame → cell) and `south`, `north`, `east`, `west` counts.
- Regenerate the two series goldens: the new column on the header and every row; the
  fixtures have no WCS and the RA axis from the trails (`series.txt`: frames 3, 5, 7 read
  `DEC 12 px`, `RA 15 px`, `DEC 28 px`; `series-classes.txt`: the axis is unknown there, so
  every cell is `-`). Any other changed line is a finding.
- Measure V-011 on Veil, record it, re-run `/validate` on all eleven cases (Analysis and
  Report are numeric code), paste the drift.

### 5. Record

- `docs/requirements.md`: R-038, R-039 → `implemented`; ADR 0013 → accepted.
- `docs/design/series-analysis.md`: `Wcs`, the flip, `MountMoves`, `MoveCell`, `StepTip`, the
  CSV columns; `docs/design/window.md`: `Tip`, the chart's hover, the column.
- `README.md`: the `dithering` column and its words, the SynScan default and the reversed-keys
  note, the tooltip, the CSV columns, the measurement in one sentence.
- `CHANGELOG.md`: Added.
- Save this plan as `docs/plans/2026-10-09-name-the-sky-direction-and-the-synscan-arrow-of-each-dither.md`;
  `/wrap-up`.

## Verification

1. `scripts/verify.ps1 -SkipTests` while editing; the full gate before the commit.
2. `dotnet test ... --filter "FullyQualifiedName~R038|FullyQualifiedName~R039|FullyQualifiedName~R025|FullyQualifiedName~R033"` green.
3. The golden diffs read line by line.
4. `LIGHTSCHECK_DUMP_SCREENS` with `WindowSmokeTests`: `tab-frames.png` shows the column; the
   tooltip is checked by `R039` on the points' `Tip` and by hand in the running window.
5. The Veil run for V-011 with its printed cells pasted.

## Outcome (2026-10-09)

Followed as written, with these notes. The `dithering` column pushed the Cadre table past the
card at 1360 px, so the table sits in a horizontal `ScrollViewer` and the last columns have
fixed widths (`fisier` 130, `dithering` 200, `motive` 280). The hand-made Veil `Wcs` in `R038`
gives DEC 128.7 px for the 128.2 px shift (the matrix's 0.0037 shear), worded `129`; the
fixture's frame 7 reads `DEC 27 px` for a 27.5 px step (formatting). V-011 counts 11 south
and 2 north DEC components over the 41 moves because `Th.AxisStep` (3 px) admits the small
ones; the hand analysis's nine DEC jumps of 12 px or more are all south.
