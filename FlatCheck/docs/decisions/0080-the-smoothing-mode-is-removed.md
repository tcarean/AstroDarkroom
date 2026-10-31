# 0080. The --smooth mode and the Smoothing tab are removed; the tool measures flats and writes no second master of them

Date: 2026-10-31
Status: accepted 2026-10-31
Supersedes: 0026; 0027; 0036 in part (the Netezire tab); 0077 in part (the Smoothing tab's lock of the folder button)

## Context

Mode 6, the smoothed flat (ADR 0026) with its scale proposal (ADR 0027), wrote a second master
with the diffuser's texture, the dust and the compact spots removed. It was built for one flat
source, fabric laid on a monitor, whose cells reach the lights at a few percent. The owner asked
on 2026-10-31 to remove the whole functionality from the console and the window and to clean up
what the removal leaves unused. Nothing in its numbers was wrong: the measurements of ADR 0026
and 0027 on the five real masters stand as history, and the frozen Python prototype never had
the mode.

## Decision

The `--smooth` mode is removed: `SmoothAnalyzer`, `SmoothScaleAdvisor`, `SmoothReport`,
`SmoothRun`, the console's `--smooth` and `--scale` options and its `-after` map, the window's
Smoothing tab (`SmoothTabViewModel`, `SmoothScaleViewModel`, `SmoothView`,
`SmoothGuideViewModel`, the smoothing block of `SessionMessages`), and the Histogram tab's
`SMOOTHED` row with its rebuild on a write (`HistEqKind.Smoothed`, `SirilSession.SmoothedSuffix`
/ `SmoothedMasterPath` / `SmoothedPathBeside`, `HistEqFileRow.IsSmoothed`,
`HistEqTabViewModel.FileWritten`, `HistEqReport.SmoothedVersusMaster`). With them go the
helpers that had no other caller: `Metrics.FineTexture` and `Metrics.BoxBlur`, `Img.TileMedians`,
`Mosaic.Extract` and `Mosaic.Merge`, the four `Th.Smooth*` thresholds, `LogText.Smooth` and
`LogText.Advice`, `FlatCheckCli.AfterMapPath`, `HistEqFileRow.ClosesGroup` (equal to `IsMaster`
once the master is alone above the frames) and the default-cap parameter of
`MapScaleViewModel`'s constructor. Requirements R-060, R-061, R-062 and R-086 are `dropped`;
R-003, R-006, R-081, R-085, R-101, R-103, R-114 and R-117 say what is left. The five validation
cases V-031 to V-035 and the ten `smooth_*` / `en_smooth_*` goldens are deleted; no parity
golden mentioned the mode, so the 39 confirmed goldens do not change. `docs/design/modes-smooth.md`
and `docs/design/smooth-validation.md` are deleted with the decision they explain.

What the mode shared stays, because another mode still calls it: `FitsWriter` and
`LoadedFrame.Cards` (the synthetic flat), `LoadedFrame.Planes` (the synthetic refusal of a
cube) and `Bitpix` (the load log line), `StructureAnalyzer.AnalyzePrepared` and
`StructureReport.TestKeys` / `Test` / `TestLabel` (the against and split reports),
`StructureRows.Tests` (the Structure tab), `Mosaic.Split` / `LooksCfa` / `PositionOf`,
`Stats.Solve`, the heavy-run gate and the overwrite delegate of `MainViewModel` (the Synthetic
tab). The R-110 test that used the `--smooth` refusal of a cube as "a refusal decided after the
load", and the R-001 copyright check on a refusal, now use the `--synthetic` refusal of a cube,
which has the same shape; the `smooth` scenario of the two-language log test is dropped.

## Alternatives rejected

- **Keep the mode in the console, drop only the tab.** A mode that exists in one application
  only breaks the "two applications over one core" rule the window was built on (ADR 0011,
  ADR 0033), as it did for `--ratio` (ADR 0067).
- **Keep `Metrics.FineTexture` as a structure figure.** It was built for the scale proposal and
  no report prints it; a metric kept for no caller is dead code, and adding it to the structure
  report would be a design of its own, with its own measurement, not a removal.
- **Keep the `SMOOTHED` row of the Histogram tab for files written by older versions.** The row
  existed to show what the tab's own button had just written; without the button it would list
  a file the tool can no longer make and never explains.
- **Keep `ClosesGroup` as the list's own flag.** With the master alone above the frames it is
  `IsMaster` under another name; the view binds to `IsMaster` and the list is no longer rebuilt
  during the session.

## Consequences

- Removing a documented console flag is a breaking change by SemVer; the version bump is the
  owner's (the kit's rule "Version and year are the owner's").
- `--smooth` and `--scale` are unknown options, exit 1 (`AFlagOfTheRemovedSmoothingModeIsAnUnknownOption`);
  `--help` lists five modes in both languages (`help.txt`, `en_help.txt`).
- The window has six tabs; `WindowSmokeTests` renders them all; the folder button is locked
  during the opening chain and the Synthetic tab's runs alone (R-117).
- The README, the user documentation, numbers the five modes consecutively (1 to 5). The
  requirements keep their ID blocks, and the design notes and the code's header comments keep
  the original mode numbers with mode 6 marked removed, as ADR 0067 did for mode 2.
- `code-map.md`, `two-applications.md`, `pipeline.md`, `thresholds.md`, `png-output.md`,
  `process.md`, `known-issues.md`, `logging.md`, `language.md` and `flat-sources.md` no longer
  describe the mode, except as history.
- Guards: `RepoInvariantTests` (R-060, R-061, R-062 and R-086 have no `R###_` test), the
  regenerated `help` goldens, `R117_TheFolderButtonIsLockedDuringASyntheticRun`.
