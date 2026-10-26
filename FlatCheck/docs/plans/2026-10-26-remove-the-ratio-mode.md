# Remove the Ratio mode (`--ratio` and the Ratio tab)

Branch `claude/Remove-the-ratio-mode`, worktree `FlatCheck/.claude/worktrees/Remove-the-ratio-mode`
(the convention the existing `Read-Lightrooms-lens-profiles-too` worktree follows). Saved to
`docs/plans/2026-10-26-remove-the-ratio-mode.md` once approved.

## Context

The owner asked to delete mode 2, the attribution by 180-degree rotation, from the console and the
window, and to clean up what the removal leaves unused. Reason (the owner, 2026-10-26): never used
in practice. Two masters from two rotated sittings are an input the workflow never produces, so the
tab stayed empty and the flag unused. Nothing is wrong with the numbers; the mode's own ADR 0019 and
its validation stay as history.

Measured facts from the survey:

- The mode is three core files, one console branch, one tab, three test files and ten goldens.
- Three helpers lose their last caller: `Img.Rotate180`, `Img.CropCentred` (only `RatioAnalyzer`
  uses them; `Metrics.RadialModel` does its own rotation), `StructureReport.WriteBody(r, w)` (the
  two-argument overload), `Th.RatioOk/RatioBad`, `FlatCheckCli.WorldMapPath`,
  `SirilSession.RatioPattern` + `IsFlatMasterName`, `SessionMessages.RatioComparing /
  RatioNeedsTwoMasters / NotAFlatMaster / SameFileTwice`. Everything else the mode touches
  (`StructureAnalyzer.AnalyzePrepared`, `Img.Divide`, `RunOutcome.SecondaryPng`, `FitsReader.HInt /
  HStr`, `ImageLoader.Load`, `StructureRows`, `MapGuideViewModel`, `MapScaleViewModel`,
  `HeavyRunGate`) has other callers and stays.
- Six other reports and the window's messages *recommend* `--ratio` / the Ratio tab as the next test.
  Those sentences must change, which changes text lines in 17 parity goldens (15 structure, 2 light)
  plus 26 ordinary goldens. The owner approved the parity recapture (2026-10-26); no figure moves.

## Decisions taken (with the owner) and assumptions

1. The removal is complete: code, tests, goldens, docs, README. The frozen Python prototype
   (`flatcheck.py`, `crosscheck.py`) stays untouched (ADR 0047).
2. Requirements `R-020` and `R-082` go to status `dropped` (the file's own rule: an ID is never
   reused; a dropped requirement keeps its ID). The R-020/R-082 tests are deleted, so
   `RepoInvariantTests` stays green.
3. New ADR `0067-the-ratio-mode-is-removed.md`: supersedes ADR 0019 whole and ADR 0033 in part
   (the Ratio tab). Both old ADRs change only their Status line (the kit's ADR 0024).
   `docs/design/modes-ratio.md` is deleted with the decision it explains.
4. Wording of the sentences that pointed at the mode (my recommendation; same meaning in both
   languages, `T()` pairs):
   - `StructureReport` dust/tilt note ("The --ratio mode separates them"): say the tool cannot
     separate a camera-fixed from a world-fixed gradient from one master; the shape on the map and a
     second session with the same lens are what tell them apart.
   - `StructureReport` reshoot advice ("run --ratio on two rotated stacks before reshooting"): drop
     the `--ratio` step; keep the "the figure is an upper bound" caution and the reshoot advice.
   - `StructureReport` radial-excess notes ("invisible to the five tests, to the map and to --ratio"):
     drop the `--ratio` clause.
   - `LightReport` / `LightSeriesReport` ("the answer comes from the 180 rotation test"): the console
     and the window say the same: on a wide field, `--light` cannot separate the flat from the sky;
     judge the flat on the structure tests alone (no replacement test is promised).
   - `SmoothReport`, `HistEqReport`, `SessionMessages` (map guide, Light reasons, Netezire note): the
     same, "only --ratio separates them" becomes "the tool cannot separate them from one master".
5. The exit codes, numbers and every other mode's output are unchanged: the only golden lines that
   change are these sentences and the help text.
6. Version: removing a documented console flag is a breaking change by SemVer. I only say so; the
   bump is the owner's (the kit's rule "Version and year are the owner's").

## Steps

### 1. Worktree and branch

`git worktree add .claude/worktrees/Remove-the-ratio-mode -b claude/Remove-the-ratio-mode main`
from `E:\Photografie\Code\FlatCheck`; work in
`.claude/worktrees/Remove-the-ratio-mode/FlatCheck` with `git -C` (never `cd` into it). Save this
plan as `docs/plans/2026-10-26-remove-the-ratio-mode.md`.

### 2. Core (`FlatCheck.Core`)

- Delete `Analysis/RatioAnalyzer.cs`, `Report/RatioReport.cs`, `Runs/RatioRun.cs`.
- `Img.cs`: delete `Rotate180` and `CropCentred` (+ their `ImgTests`).
- `Thresholds.cs`: delete `RatioOk`, `RatioBad`; fix the class summary that lists the modes.
- `Report/StructureReport.cs`: delete the two-argument `WriteBody`; reword the sentences of step 4.
- `Report/LightReport.cs`, `LightSeriesReport.cs`, `SmoothReport.cs`, `HistEqReport.cs`: reword.
- Comments naming the mode: `VignettingComparison.cs`, `ImageLoader.cs`, `PngWriter.cs`,
  `Runs/RunOutcome.cs` (`SecondaryPng` now only `--smooth`), `Runs/AgainstRun.cs` header.

### 3. Console (`FlatCheck`)

- `Options.cs`: field `Ratio`, the `--ratio` case and the list of flags taking a value.
- `FlatCheckCli.cs`: dispatch branch, `RunRatio`, `WorldMapPath` (and the comment in
  `AfterMapPath` that cites it), the two usage lines, the dispatch-order comment.

### 4. Window (`FlatCheck.Wpf`)

- Delete `ViewModels/RatioTabViewModel.cs`, `Views/RatioView.xaml` + `.xaml.cs`.
- `MainWindow.xaml`: the `TabItem`; `MainWindow.xaml.cs`, `MainViewModel.cs`: the property,
  construction, `Attach`, `Reset`, the "eight tabs" comments → seven.
- `Session/SessionMessages.cs`: delete `RatioComparing`, `RatioNeedsTwoMasters`, `NotAFlatMaster`,
  `SameFileTwice`; reword the three guide/reason texts that name the Ratio tab.
- `Session/SirilSession.cs`: delete `RatioPattern`, `IsFlatMasterName`; fix the doc comments that
  cite them (the smoothed master, the second stack in `process/`).
- Comments: `TabViewModel.cs`, `HeavyRunGate.cs`, `MapGuideViewModel.cs`, `MapScaleViewModel.cs`,
  `StructureRows.cs`, `StructureTabViewModel.cs`, `MapGuidePanel.xaml(.cs)`, `StructureView.xaml`
  (the GroupName remark), `Theme/Dark.xaml`, `Views/LightView.xaml`.

### 5. Tests and goldens

- Delete `FlatCheck.Tests/RatioAnalyzerTests.cs`, `RatioReportTests.cs`,
  `FlatCheck.Wpf.Tests/RatioTabTests.cs`.
- Remove the Ratio cases from `GoldenOutputTests` (4 tests), `EnglishOutputTests` (2 scenarios),
  `OptionsTests` (3), `RunsTests` (2), `LanguageTests` (use another flag), `VersionTests` (2 lines),
  `ImgTests` (Rotate180/CropCentred), `ParityGoldenTests` (4 rows), `MainViewModelTests` (3),
  `WindowLanguageTests`, `WindowSmokeTests` ("eight tabs" → seven), `SirilSessionTests`
  (`IsFlatMasterName` tests), `SmoothTabTests` (one assert), `LightTabTests` (`Reasons` assert on
  "Ratio"), and the `--ratio` asserts in `LightReportTests`, `LightSeriesReportTests`,
  `StructureReportTests`, `HistEqReportTests` (replaced by asserts on the new wording).
- Delete goldens `ratio_camera`, `ratio_world`, `en_ratio_*`, `parity_ratio_*` (4).
- Regenerate the ordinary goldens (`FLATCHECK_UPDATE_GOLDEN=1 ... --filter GoldenOutputTests`),
  then the parity goldens (`--filter ParityGoldenTests`, approved 2026-10-26). Read the whole
  golden diff; every changed line must be one of the reworded sentences or the help text.

### 6. Documentation

- `docs/requirements.md`: R-020 and R-082 `dropped` (text kept, prefixed "Dropped 2026-10-26, ADR
  0067"); edit R-003 (dispatch order), R-006 and R-103 (second map: only `-after`), R-081 (list of
  click-started runs), R-083 (the Ratio-tab wording), R-098 (43 → 39 scenarios, no `--ratio`), the
  "Mode 2" heading.
- `docs/decisions/0067-the-ratio-mode-is-removed.md`; Status lines of 0019 and 0033.
- `docs/design/`: delete `modes-ratio.md`; its row in `README.md`; `code-map.md` (the analyzer,
  report, run, result table, `RunOutcome` table, the tab), `two-applications.md` (input/map rows, the
  "one exception" paragraph, the gate list), `pipeline.md` (dispatch order, "The seven modes" → six),
  `python-parity.md` (39 scenarios, the recapture of 2026-10-26 recorded as a C#-only change with
  the lines and their cause), `thresholds.md` (two rows, the intro), `png-output.md` (`-world`),
  `process.md` (the `Options` fields, the dispatch order, `ratio_world` example, the mode list),
  `known-issues.md` (two mentions), `language.md` if it names the tab.
- `docs/validation.md`: drop the "`--ratio` figure 4.02 %" bullet. `validation.json` is untouched
  (`ratioResidualPtpPct` belongs to `--against`).
- `CLAUDE.md`: "Seven modes" → six, the run example. `CHANGELOG.md`: `### Removed` under
  `[Unreleased]`, with the breaking-change note.
- `README.md` (Romanian, user documentation): section "2. Atribuire prin rotație 180°" removed, the
  mode table (line 49), "Cele șapte moduri" → șase, the cross-references at lines 148, 163, 198, 523,
  539, 723, 755, the thresholds table (790-803), the usage and `-o` option text (836-850), the PNG
  section (895-899), the shapes table (946-963), the window section ("Cele opt taburi" → șapte, the
  Ratio tab rows 1107, 1136, 1173, 1317), "Golden-urile de paritate" (count).

### 7. Gate and commit

- `dotnet build FlatCheck.sln -c Release --no-incremental -warnaserror --no-restore` first (an
  incremental build hides warnings from the gate), then `scripts/verify.ps1`.
- `scripts/check-design.ps1 -Since main` for the notes touched.
- No formula moved, so `/validate` is not required; the parity recapture is reported with its diff.
- Small commits on the branch (core, console, window, tests+goldens, docs); `/wrap-up` proposes the
  merge. Say that the change warrants a version bump; the owner decides.

## Verification

- `dotnet test FlatCheck.sln -c Release --filter "Category!=Validation"` green, `RepoInvariantTests`
  included (no `R020_`/`R082_` test, both rows `dropped`).
- `grep -ri "ratio"` over the code finds only aspect ratio, `--against`'s ratio raster, the rule
  "Ratios" and `ratioResidualPtpPct`.
- `flatcheck a.fit --ratio b.fit` is an unknown-option error; `--help` in both languages has no
  `--ratio` line (goldens `help.txt`, `en_help.txt`).
- `FLATCHECK_DUMP_SCREENS=<dir> dotnet test FlatCheck.Wpf.Tests --filter WindowSmokeTests` renders
  seven tabs, no Ratio tab.
- The golden diff, read whole: only the reworded sentences and the help lines changed, no figure.
